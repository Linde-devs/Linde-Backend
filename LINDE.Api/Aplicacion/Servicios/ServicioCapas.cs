using LINDE.Api.Auditoria;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Importacion;

namespace LINDE.Api.Servicios;

/// <summary>CU-11: el administrador carga las capas del INRA y de la ABT como archivos y las consulta.</summary>
public interface IServicioCapas
{
    IReadOnlyList<CapaReferencia> Listar();
    CapaReferencia Cargar(TipoCapa tipo, string nombre, string fuente, string archivo, Stream contenido);
    IReadOnlyList<ElementoCapa> Elementos(int capaId);
    void Eliminar(int id);
}

public class ServicioCapas(
    IRepositorio<CapaReferencia> capas,
    IRepositorio<ElementoCapa> elementos,
    FabricaLectoresCapa fabrica,
    IUsuarioActual usuario) : IServicioCapas
{
    public IReadOnlyList<CapaReferencia> Listar() =>
        capas.Listar().OrderByDescending(c => c.Vigente).ThenByDescending(c => c.FechaCarga).ToList();

    /// <summary>
    /// Lee el archivo con el lector que entrega la fábrica, guarda sus polígonos y deja esta capa como la vigente
    /// de su tipo. La capa anterior del mismo tipo queda en el historial, sin elementos.
    /// </summary>
    public CapaReferencia Cargar(TipoCapa tipo, string nombre, string fuente, string archivo, Stream contenido)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DatosInvalidosException("Escribe un nombre para la capa.", "nombre");
        if (string.IsNullOrWhiteSpace(fuente))
            throw new DatosInvalidosException("Indica la fuente de la capa (institución y fecha de corte).", "fuente");

        // Se lee todo antes de tocar la base: si el archivo tiene un error, la capa vigente no cambia.
        var leidos = fabrica.CrearLector(archivo).Leer(contenido);

        foreach (var anterior in capas.Listar(c => c.Tipo == tipo && c.Vigente))
        {
            foreach (var e in elementos.Listar(e => e.CapaId == anterior.Id))
                elementos.Eliminar(e.Id);
            anterior.Vigente = false;
            capas.Actualizar(anterior);
        }

        var capa = capas.Agregar(new CapaReferencia
        {
            Tipo = tipo,
            Nombre = nombre.Trim(),
            Fuente = fuente.Trim(),
            Archivo = Path.GetFileName(archivo),
            CantidadElementos = leidos.Count,
            CargadaPor = usuario.Nombre
        });

        foreach (var l in leidos)
            elementos.Agregar(new ElementoCapa
            {
                CapaId = capa.Id,
                Tipo = tipo,
                Codigo = l.Codigo,
                TitularCi = l.TitularCi,
                SuperficieHa = l.SuperficieHa,
                Geometria = l.Geometria
            });

        return capa;
    }

    public IReadOnlyList<ElementoCapa> Elementos(int capaId)
    {
        _ = capas.Obtener(capaId) ?? throw new RecursoNoEncontradoException($"No existe la capa {capaId}.");
        return elementos.Listar(e => e.CapaId == capaId);
    }

    /// <summary>Quita la capa y sus elementos. Sin capa vigente, el cruce vuelve a usar los datos declarados.</summary>
    public void Eliminar(int id)
    {
        var capa = capas.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe la capa {id}.");
        foreach (var e in elementos.Listar(e => e.CapaId == capa.Id))
            elementos.Eliminar(e.Id);
        capas.Eliminar(id);
    }
}

/// <summary>Decorator de auditoría: la carga y el borrado de capas quedan en la bitácora.</summary>
public class ServicioCapasAuditado(IServicioCapas interno, IRepositorio<CapaReferencia> capas, AuditorOperaciones auditor) : IServicioCapas
{
    private const string Entidad = "CapaReferencia";

    public IReadOnlyList<CapaReferencia> Listar() => interno.Listar();

    public CapaReferencia Cargar(TipoCapa tipo, string nombre, string fuente, string archivo, Stream contenido) =>
        auditor.Ejecutar($"Cargar capa ({tipo}, {Path.GetFileName(archivo)})", Entidad, null, null,
            () => interno.Cargar(tipo, nombre, fuente, archivo, contenido), c => c.Id.ToString());

    public IReadOnlyList<ElementoCapa> Elementos(int capaId) => interno.Elementos(capaId);

    public void Eliminar(int id) =>
        auditor.Ejecutar("Eliminar capa", Entidad, id.ToString(), AuditorOperaciones.Instantanea(capas.Obtener(id)),
            () => interno.Eliminar(id));
}
