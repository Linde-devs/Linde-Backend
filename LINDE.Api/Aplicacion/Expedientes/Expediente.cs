using LINDE.Api.Dominio;

namespace LINDE.Api.Expedientes;

/// <summary>Estado del expediente: conforme (las cuatro capas sin observaciones) o con alertas.</summary>
public enum EstadoInforme
{
    Conforme,
    ConAlertas
}

/// <summary>Una alerta tal como queda escrita en el expediente.</summary>
public record AlertaExpediente(CapaValidacion Capa, PrioridadAlerta Prioridad, string Mensaje);

/// <summary>RF-14. Inspección de campo tal como queda en el expediente: fecha, técnico, resultado y comentario.</summary>
public record InspeccionExpediente(DateOnly Fecha, string Tecnico, ResultadoInspeccion Resultado, string? Comentario);

/// <summary>
/// Expediente digital del predio (CU-10, RF-09). Se genera para predios aprobados y también para predios con alertas,
/// indicando su estado. El productor lo muestra al técnico o al SENASAG, que pueden consultarlo por su código
/// sin tener cuenta. No es la certificación: la emite el SENASAG fuera del sistema.
/// </summary>
public class Expediente : IEntidad
{
    public const string Leyenda =
        "LINDE no emite certificaciones. Este expediente reúne la evidencia declarada y el resultado de las validaciones " +
        "automáticas. El análisis satelital es informativo y tiene un margen de error de ±5 % a ±20 %.";

    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public DateTime FechaGeneracion { get; set; } = DateTime.UtcNow;
    public string Productor { get; set; } = "";
    public string CiProductor { get; set; } = "";
    public int PredioId { get; set; }
    public string Predio { get; set; } = "";
    public string Departamento { get; set; } = "";
    public string Municipio { get; set; } = "";
    public double SuperficieDeclaradaHa { get; set; }
    public double AreaPoligonoHa { get; set; }
    public int VersionPoligono { get; set; }
    public List<Coordenada> Vertices { get; set; } = new();
    public List<ResultadoCapa> Validaciones { get; set; } = new();
    public List<string> Documentos { get; set; } = new();
    public EstadoInforme Estado { get; set; }
    public List<AlertaExpediente> Alertas { get; set; } = new();
    public List<InspeccionExpediente> Inspecciones { get; set; } = new();
}
