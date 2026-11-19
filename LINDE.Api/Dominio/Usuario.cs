namespace LINDE.Api.Dominio;

/// <summary>Los cuatro roles del sistema (RNF-03).</summary>
public enum RolUsuario
{
    Productor,
    Verificador,
    Senasag,
    Administrador
}

/// <summary>Cuenta de acceso. La contraseña nunca se guarda: solo su hash.</summary>
public class Usuario : IEntidad
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string Ci { get; set; } = "";
    public string Correo { get; set; } = "";
    public string ContrasenaHash { get; set; } = "";
    public RolUsuario Rol { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
