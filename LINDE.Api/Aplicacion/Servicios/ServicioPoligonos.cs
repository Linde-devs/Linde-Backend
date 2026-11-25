using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using LINDE.Api.Importacion;

namespace LINDE.Api.Servicios;

/// <summary>CU-04: registrar, importar y editar el polígono de un predio, conservando cada versión.</summary>
public interface IServicioPoligonos
{
    Poligono? ObtenerVigente(int predioId);
    IReadOnlyList<Poligono> Historial(int predioId);
    Poligono Registrar(int predioId, IReadOnlyList<Coordenada> vertices, string? motivo, IReadOnlyList<double?>? precisionesM = null);
    Poligono Importar(int predioId, string nombreArchivo, Stream contenido, string? motivo);
    Poligono EditarVertice(int predioId, int indice, Coordenada nueva, string? motivo);
}

public class ServicioPoligonos(
    IRepositorio<Predio> predios,
    IRepositorio<Poligono> poligonos,
    FabricaLectores fabrica,
    IUsuarioActual usuario,
    ControlAcceso acceso) : IServicioPoligonos
{
    public Poligono? ObtenerVigente(int predioId)
    {
        PredioAccesible(predioId);
        return poligonos.Listar(p => p.PredioId == predioId && p.Vigente).FirstOrDefault();
    }

    public IReadOnlyList<Poligono> Historial(int predioId)
    {
        PredioAccesible(predioId);
        return poligonos.Listar(p => p.PredioId == predioId).OrderByDescending(p => p.Version).ToList();
    }

    public const double PrecisionMaximaM = 10_000;

    public Poligono Registrar(int predioId, IReadOnlyList<Coordenada> vertices, string? motivo, IReadOnlyList<double?>? precisionesM = null)
    {
        if (vertices.Count == 0)
            throw new GeometriaInvalidaException(new ProblemaGeometria(ErrorGeometria.SinVertices, "Envía al menos un vértice."));
        var precisiones = Precisiones(vertices.Count, precisionesM);

        // RF-05: un punto se guarda con 6 decimales de latitud y longitud (unos 11 cm).
        var lista = vertices.Count == 1
            ? [new Coordenada(Math.Round(vertices[0].Lon, 6), Math.Round(vertices[0].Lat, 6))]
            : vertices.ToList();

        return NuevaVersion(predioId, motivo ?? (lista.Count == 1 ? "Registro del punto" : "Registro del polígono"),
            copia =>
            {
                copia.Vertices = lista;
                copia.PrecisionesM = precisiones;
            });
    }

    /// <summary>
    /// RF-04. Sin precisiones (archivo o dibujo con clics) la lista queda vacía. Si vienen, una por vértice: null para
    /// los marcados a mano y, para los del GPS, un valor en metros mayor a cero, redondeado a 0,1 m.
    /// </summary>
    private static List<double?> Precisiones(int cantidad, IReadOnlyList<double?>? precisiones)
    {
        if (precisiones is null || precisiones.All(p => p is null)) return new();
        if (precisiones.Count != cantidad)
            throw new DatosInvalidosException($"Se enviaron {precisiones.Count} precisiones para {cantidad} vértices.", "vertices");
        if (precisiones.Any(p => p is <= 0 or > PrecisionMaximaM or double.NaN))
            throw new DatosInvalidosException($"La precisión del GPS debe estar entre 0 y {PrecisionMaximaM:0} m.", "vertices");
        return precisiones.Select(p => p is double m ? Math.Round(m, 1) : (double?)null).ToList();
    }

    /// <summary>Factory Method: la fábrica entrega el lector según la extensión; aquí no se sabe cuál es.</summary>
    public Poligono Importar(int predioId, string nombreArchivo, Stream contenido, string? motivo)
    {
        var lector = fabrica.CrearLector(nombreArchivo);
        var vertices = lector.Leer(contenido);
        return Registrar(predioId, vertices, motivo ?? $"Importado desde {lector.Formato} ({nombreArchivo})");
    }

    public Poligono EditarVertice(int predioId, int indice, Coordenada nueva, string? motivo) =>
        NuevaVersion(predioId, motivo ?? $"Corrección del vértice {indice}", copia =>
        {
            if (indice < 0 || indice >= copia.Vertices.Count)
                throw new DatosInvalidosException($"El vértice {indice} no existe.");

            var ultimo = copia.Vertices.Count - 1;
            var esCierre = copia.Vertices.Count > 1 && copia.Vertices[0] == copia.Vertices[ultimo];
            copia.Vertices[indice] = nueva;
            // RF-04: un vértice corregido a mano ya no tiene la precisión del GPS.
            if (copia.PrecisionesM.Count == copia.Vertices.Count) copia.PrecisionesM[indice] = null;

            // El primer y el último vértice son el mismo punto: se mueven juntos para que el anillo siga cerrado.
            if (esCierre && indice == 0) copia.Vertices[ultimo] = nueva;
            if (esCierre && indice == ultimo) copia.Vertices[0] = nueva;
            if (esCierre && copia.PrecisionesM.Count == copia.Vertices.Count && (indice == 0 || indice == ultimo))
                copia.PrecisionesM[0] = copia.PrecisionesM[ultimo] = null;
        });

    /// <summary>
    /// Patrón Prototype: se clona la versión vigente (copia profunda), se modifica la copia
    /// y la original queda intacta en el historial.
    /// </summary>
    private Poligono NuevaVersion(int predioId, string motivo, Action<Poligono> modificar)
    {
        var predio = PredioAccesible(predioId);
        predio.VerificarEdicion();

        var vigente = ObtenerVigente(predioId);
        var nueva = vigente?.Clonar() ?? new Poligono { PredioId = predioId, Version = 0 };

        modificar(nueva);

        // RF-03: una geometría inválida se rechaza en el momento de cargarla, indicando el tipo de error.
        // La superposición con otros predios (RF-06) se revisa al enviar a revisión.
        if (RevisionGeometrica.Revisar(nueva.Vertices) is { } problema)
            throw new GeometriaInvalidaException(problema);
        nueva.Version += 1;
        nueva.Fecha = DateTime.UtcNow;
        nueva.Autor = usuario.Nombre;
        nueva.Motivo = motivo;
        nueva.Vigente = true;

        if (vigente is not null)
        {
            vigente.Vigente = false;
            poligonos.Actualizar(vigente);
        }

        var guardada = poligonos.Agregar(nueva);

        // Con otro polígono, el resultado del último envío ya no vale: el predio vuelve a "Registrado".
        predio.RegistrarModificacion();
        predios.Actualizar(predio);
        return guardada;
    }

    private Predio PredioAccesible(int predioId)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        acceso.VerificarPredio(predio);
        return predio;
    }
}

/// <summary>Patrón Decorator: auditoría del CU-04. El historial guarda las geometrías; la bitácora guarda quién hizo cada operación.</summary>
public class ServicioPoligonosAuditado(IServicioPoligonos interno, AuditorOperaciones auditor) : IServicioPoligonos
{
    private const string Entidad = "Poligono";

    public Poligono? ObtenerVigente(int predioId) => interno.ObtenerVigente(predioId);

    public IReadOnlyList<Poligono> Historial(int predioId) => interno.Historial(predioId);

    public Poligono Registrar(int predioId, IReadOnlyList<Coordenada> vertices, string? motivo, IReadOnlyList<double?>? precisionesM = null) =>
        auditor.Ejecutar("Registrar polígono", Entidad, $"predio {predioId}", Anterior(predioId),
            () => interno.Registrar(predioId, vertices, motivo, precisionesM), Descripcion);

    public Poligono Importar(int predioId, string nombreArchivo, Stream contenido, string? motivo) =>
        auditor.Ejecutar($"Importar polígono ({nombreArchivo})", Entidad, $"predio {predioId}", Anterior(predioId),
            () => interno.Importar(predioId, nombreArchivo, contenido, motivo), Descripcion);

    public Poligono EditarVertice(int predioId, int indice, Coordenada nueva, string? motivo) =>
        auditor.Ejecutar($"Editar vértice {indice}", Entidad, $"predio {predioId}", Anterior(predioId),
            () => interno.EditarVertice(predioId, indice, nueva, motivo), Descripcion);

    private string? Anterior(int predioId) => AuditorOperaciones.Instantanea(interno.ObtenerVigente(predioId));

    private static string Descripcion(Poligono p) => $"predio {p.PredioId}, versión {p.Version}";
}
