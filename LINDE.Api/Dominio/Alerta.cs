namespace LINDE.Api.Dominio;

public class Alerta : IEntidad
{
    public int Id { get; set; }
    public int PredioId { get; set; }
    public CapaValidacion Capa { get; set; }
    public PrioridadAlerta Prioridad { get; set; }
    public string Mensaje { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public bool Resuelta { get; set; }
}
