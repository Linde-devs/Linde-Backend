namespace LINDE.Api.Dominio;

/// <summary>Resultado de una sola capa de validación.</summary>
public class ResultadoCapa
{
    public CapaValidacion Capa { get; init; }
    public EstadoCapa Estado { get; init; }
    public string Mensaje { get; init; } = "";

    /// <summary>
    /// Prioridad de la alerta, cuando la capa la decide según el problema. Si es null se usa la de la capa
    /// (Alta para uso de suelo y cobertura, Media para las demás).
    /// </summary>
    public PrioridadAlerta? Prioridad { get; init; }

    /// <summary>Solo para la capa de uso permitido.</summary>
    public EstadoUsoSuelo? UsoSuelo { get; init; }
}

/// <summary>Resultado completo de un envío a revisión: una entrada por cada capa que se ejecutó.</summary>
public class ResultadoValidacion : IEntidad
{
    public int Id { get; set; }
    public int PredioId { get; set; }
    public int VersionPoligono { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public List<ResultadoCapa> Capas { get; set; } = new();

    /// <summary>True si la capa geométrica detuvo la cadena.</summary>
    public bool Cortado { get; set; }

    public EstadoExpediente EstadoResultante { get; set; }
    public string Mensaje { get; set; } = "";
}
