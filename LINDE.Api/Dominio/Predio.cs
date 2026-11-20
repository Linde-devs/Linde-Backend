using LINDE.Api.Dominio.Estados;

namespace LINDE.Api.Dominio;

/// <summary>
/// Predio agrícola declarado por un productor.
/// Patrón State: el predio no decide qué operaciones se permiten; delega en su estado actual.
/// </summary>
public class Predio : IEntidad
{
    public int Id { get; set; }
    public int ProductorId { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Departamento de Bolivia donde está el predio; el municipio debe pertenecer a él.</summary>
    public string Departamento { get; set; } = "Santa Cruz";
    public string Municipio { get; set; } = "";

    /// <summary>Cultivo principal del predio (soya, café, cacao, etc.).</summary>
    public string Cultivo { get; set; } = "";

    /// <summary>Superficie declarada en hectáreas, con 2 decimales como máximo (RF-02).</summary>
    public double SuperficieDeclaradaHa { get; set; }

    /// <summary>Número del título de propiedad emitido por el INRA, si el predio está titulado.</summary>
    public string? NumeroTituloInra { get; set; }

    /// <summary>Superficie de desmonte que declara el productor, si la hay (se usa para la Ley 741).</summary>
    public double? DesmonteDeclaradoHa { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    /// <summary>Se guarda como valor de la enumeración; el patrón organiza el comportamiento, no el almacenamiento.</summary>
    public EstadoExpediente Estado { get; private set; } = EstadoExpediente.Registrado;

    private IEstadoPredio EstadoActual => EstadosPredio.Para(Estado);

    public void VerificarEdicion() => EstadoActual.VerificarEdicion();

    public void VerificarEnvio() => EstadoActual.VerificarEnvio();

    public void EnviarARevision(bool hayAlertas) => Estado = EstadoActual.EnviarARevision(hayAlertas);

    public void VerificarGeneracionExpediente() => EstadoActual.VerificarGeneracionExpediente();
}
