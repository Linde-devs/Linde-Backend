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
    public string Municipio { get; set; } = "";
    public double SuperficieDeclaradaHa { get; set; }

    /// <summary>Superficie de desmonte que declara el productor, si la hay (se usa para la Ley 741).</summary>
    public double? DesmonteDeclaradoHa { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    /// <summary>Se guarda como valor de la enumeración; el patrón organiza el comportamiento, no el almacenamiento.</summary>
    public EstadoExpediente Estado { get; private set; } = EstadoExpediente.Registrado;

    public string? ComentarioVerificador { get; private set; }

    private IEstadoPredio EstadoActual => EstadosPredio.Para(Estado);

    public void VerificarEdicion() => EstadoActual.VerificarEdicion();

    public void VerificarEnvio() => EstadoActual.VerificarEnvio();

    public void EnviarARevision(bool hayAlertas) => Estado = EstadoActual.EnviarARevision(hayAlertas);

    public void Aprobar(string? comentario)
    {
        Estado = EstadoActual.Aprobar();
        ComentarioVerificador = comentario;
    }

    public void Observar(string comentario)
    {
        if (string.IsNullOrWhiteSpace(comentario))
            throw new DatosInvalidosException("El comentario es obligatorio al observar un predio.");

        Estado = EstadoActual.Observar();
        ComentarioVerificador = comentario.Trim();
    }

    public void VerificarGeneracionExpediente() => EstadoActual.VerificarGeneracionExpediente();
}
