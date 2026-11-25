namespace LINDE.Api.Dominio;

/// <summary>Registro de auditoría: quién hizo qué, cuándo y con qué valores anteriores.</summary>
public class RegistroBitacora : IEntidad
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Usuario { get; set; } = "";
    public string Operacion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public string? EntidadId { get; set; }
    public string? ValoresAnteriores { get; set; }
    public string Resultado { get; set; } = "";
}
