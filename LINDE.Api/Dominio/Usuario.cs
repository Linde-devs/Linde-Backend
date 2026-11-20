namespace LINDE.Api.Dominio;

/// <summary>
/// Los dos roles del sistema (RNF-03). El verificador y el SENASAG no tienen cuenta:
/// revisan el expediente que les muestra el productor o lo consultan por su código.
/// </summary>
public enum RolUsuario
{
    Productor,
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

    /// <summary>Solo para el rol Productor: el productor al que pertenece esta cuenta.</summary>
    public int? ProductorId { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
