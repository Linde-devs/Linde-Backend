using LINDE.Api.Auditoria;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Importacion;

namespace LINDE.Api.Servicios;

/// <summary>CU-04: registrar, importar y editar el polígono de un predio, conservando cada versión.</summary>
public interface IServicioPoligonos
{
    Poligono? ObtenerVigente(int predioId);
    IReadOnlyList<Poligono> Historial(int predioId);
    Poligono Registrar(int predioId, IReadOnlyList<Coordenada> vertices, string? motivo);
    Poligono Importar(int predioId, string nombreArchivo, Stream contenido, string? motivo);
    Poligono EditarVertice(int predioId, int indice, Coordenada nueva, string? motivo);
}

public class ServicioPoligonos(
    IRepositorio<Predio> predios,
    IRepositorio<Poligono> poligonos,
    FabricaLectores fabrica,
    IUsuarioActual usuario) : IServicioPoligonos
{
    public Poligono? ObtenerVigente(int predioId) =>
        poligonos.Listar(p => p.PredioId == predioId && p.Vigente).FirstOrDefault();

    public IReadOnlyList<Poligono> Historial(int predioId) =>
        poligonos.Listar(p => p.PredioId == predioId).OrderByDescending(p => p.Version).ToList();

    public Poligono Registrar(int predioId, IReadOnlyList<Coordenada> vertices, string? motivo)
    {
        if (vertices.Count == 0)
            throw new DatosInvalidosException("Envía al menos un vértice.");

        return NuevaVersion(predioId, motivo ?? "Registro del polígono", copia => copia.Vertices = vertices.ToList());
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

            // El primer y el último vértice son el mismo punto: se mueven juntos para que el anillo siga cerrado.
            if (esCierre && indice == 0) copia.Vertices[ultimo] = nueva;
            if (esCierre && indice == ultimo) copia.Vertices[0] = nueva;
        });

    /// <summary>
    /// Patrón Prototype: se clona la versión vigente (copia profunda), se modifica la copia
    /// y la original queda intacta en el historial.
    /// </summary>
    private Poligono NuevaVersion(int predioId, string motivo, Action<Poligono> modificar)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        predio.VerificarEdicion();

        var vigente = ObtenerVigente(predioId);
        var nueva = vigente?.Clonar() ?? new Poligono { PredioId = predioId, Version = 0 };

        modificar(nueva);
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

        return poligonos.Agregar(nueva);
    }
}

/// <summary>Patrón Decorator: auditoría del CU-04. El historial guarda las geometrías; la bitácora guarda quién hizo cada operación.</summary>
public class ServicioPoligonosAuditado(IServicioPoligonos interno, AuditorOperaciones auditor) : IServicioPoligonos
{
    private const string Entidad = "Poligono";

    public Poligono? ObtenerVigente(int predioId) => interno.ObtenerVigente(predioId);

    public IReadOnlyList<Poligono> Historial(int predioId) => interno.Historial(predioId);

    public Poligono Registrar(int predioId, IReadOnlyList<Coordenada> vertices, string? motivo) =>
        auditor.Ejecutar("Registrar polígono", Entidad, $"predio {predioId}", Anterior(predioId),
            () => interno.Registrar(predioId, vertices, motivo), Descripcion);

    public Poligono Importar(int predioId, string nombreArchivo, Stream contenido, string? motivo) =>
        auditor.Ejecutar($"Importar polígono ({nombreArchivo})", Entidad, $"predio {predioId}", Anterior(predioId),
            () => interno.Importar(predioId, nombreArchivo, contenido, motivo), Descripcion);

    public Poligono EditarVertice(int predioId, int indice, Coordenada nueva, string? motivo) =>
        auditor.Ejecutar($"Editar vértice {indice}", Entidad, $"predio {predioId}", Anterior(predioId),
            () => interno.EditarVertice(predioId, indice, nueva, motivo), Descripcion);

    private string? Anterior(int predioId) => AuditorOperaciones.Instantanea(interno.ObtenerVigente(predioId));

    private static string Descripcion(Poligono p) => $"predio {p.PredioId}, versión {p.Version}";
}
