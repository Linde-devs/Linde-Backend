namespace LINDE.Api.Dominio;

/// <summary>RF-01. Productor agrícola. Se registra junto con su cuenta de acceso (rol Productor).</summary>
public class Productor : IEntidad
{
    public int Id { get; set; }

    /// <summary>Código único asignado al registrarse, por ejemplo PRD-000001.</summary>
    public string Codigo { get; set; } = "";

    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";

    /// <summary>CI o NIT. No se puede repetir entre productores.</summary>
    public string Ci { get; set; } = "";

    /// <summary>Código del Registro Único Nacional de Productores Agropecuarios, si lo tiene.</summary>
    public string? Runpa { get; set; }

    public string Telefono { get; set; } = "";
    public string Municipio { get; set; } = "";

    /// <summary>Cuenta con la que el productor inicia sesión.</summary>
    public int UsuarioId { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
