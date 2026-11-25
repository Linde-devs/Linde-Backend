namespace LINDE.Api.Dominio;

/// <summary>
/// RF-14 (CU-09). Inspección de campo hecha por un técnico verificador. La registra el productor con lo que anotó el
/// técnico; LINDE no la valida: queda como evidencia en la Vista para revisión y en el expediente.
/// </summary>
public class InspeccionCampo : IEntidad
{
    public int Id { get; set; }
    public int PredioId { get; set; }

    /// <summary>Día de la visita al predio.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Nombre del técnico que hizo la inspección.</summary>
    public string Tecnico { get; set; } = "";

    public ResultadoInspeccion Resultado { get; set; }
    public string? Comentario { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    /// <summary>Correo de quien la registró en LINDE.</summary>
    public string RegistradaPor { get; set; } = "";
}
