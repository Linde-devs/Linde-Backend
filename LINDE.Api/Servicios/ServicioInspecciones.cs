using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Servicios;

/// <summary>RF-14 (CU-09). Inspecciones de campo del predio.</summary>
public interface IServicioInspecciones
{
    IReadOnlyList<InspeccionCampo> Listar(int predioId);
    InspeccionCampo Registrar(int predioId, InspeccionDto dto);
    void Eliminar(int id);
}

public class ServicioInspecciones(
    IRepositorio<Predio> predios,
    IRepositorio<InspeccionCampo> inspecciones,
    ControlAcceso acceso,
    IUsuarioActual usuario) : IServicioInspecciones
{
    public const int LargoTecnico = 100;
    public const int LargoComentario = 1000;

    public IReadOnlyList<InspeccionCampo> Listar(int predioId)
    {
        PredioAccesible(predioId);
        return inspecciones.Listar(i => i.PredioId == predioId).OrderByDescending(i => i.Fecha).ThenByDescending(i => i.Id).ToList();
    }

    /// <summary>
    /// La inspección no cambia el polígono ni los datos del predio, por eso se puede registrar en cualquier estado,
    /// también con el predio aprobado. Para que aparezca en el expediente hay que volver a generarlo.
    /// </summary>
    public InspeccionCampo Registrar(int predioId, InspeccionDto dto)
    {
        PredioAccesible(predioId);

        if (dto.Fecha is not { } fecha)
            throw new DatosInvalidosException("Indica la fecha de la inspección.", "fecha");
        // Bolivia está en UTC-4 todo el año: así "hoy" es el mismo día que ve el productor.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));
        if (fecha > hoy)
            throw new DatosInvalidosException("La fecha de la inspección no puede ser posterior a hoy.", "fecha");

        var tecnico = dto.Tecnico?.Trim() ?? "";
        if (tecnico.Length == 0)
            throw new DatosInvalidosException("Escribe el nombre del técnico que hizo la inspección.", "tecnico");
        if (tecnico.Length > LargoTecnico)
            throw new DatosInvalidosException($"El nombre del técnico admite hasta {LargoTecnico} caracteres.", "tecnico");

        if (dto.Resultado is not { } resultado || !Enum.IsDefined(resultado))
            throw new DatosInvalidosException("Elige el resultado de la inspección.", "resultado");

        var comentario = string.IsNullOrWhiteSpace(dto.Comentario) ? null : dto.Comentario.Trim();
        if (comentario is null && resultado != ResultadoInspeccion.Conforme)
            throw new DatosInvalidosException("Anota lo que observó el técnico: es obligatorio si el resultado no es conforme.", "comentario");
        if (comentario?.Length > LargoComentario)
            throw new DatosInvalidosException($"El comentario admite hasta {LargoComentario} caracteres.", "comentario");

        return inspecciones.Agregar(new InspeccionCampo
        {
            PredioId = predioId,
            Fecha = fecha,
            Tecnico = tecnico,
            Resultado = resultado,
            Comentario = comentario,
            RegistradaPor = usuario.Nombre
        });
    }

    public void Eliminar(int id)
    {
        var inspeccion = inspecciones.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe la inspección {id}.");
        PredioAccesible(inspeccion.PredioId);
        inspecciones.Eliminar(id);
    }

    private Predio PredioAccesible(int predioId)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        acceso.VerificarPredio(predio);
        return predio;
    }
}

/// <summary>Patrón Decorator: auditoría de las inspecciones de campo.</summary>
public class ServicioInspeccionesAuditado(
    IServicioInspecciones interno,
    IRepositorio<InspeccionCampo> inspecciones,
    AuditorOperaciones auditor) : IServicioInspecciones
{
    private const string Entidad = "InspeccionCampo";

    public IReadOnlyList<InspeccionCampo> Listar(int predioId) => interno.Listar(predioId);

    public InspeccionCampo Registrar(int predioId, InspeccionDto dto) =>
        auditor.Ejecutar($"Registrar inspección de campo (predio {predioId})", Entidad, null, null,
            () => interno.Registrar(predioId, dto), i => i.Id.ToString());

    public void Eliminar(int id) =>
        auditor.Ejecutar("Eliminar inspección de campo", Entidad, id.ToString(),
            AuditorOperaciones.Instantanea(inspecciones.Obtener(id)), () => interno.Eliminar(id));
}
