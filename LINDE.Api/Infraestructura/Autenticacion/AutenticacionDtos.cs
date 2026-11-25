using LINDE.Api.Dominio;

namespace LINDE.Api.Autenticacion;

/// <summary>Registro de otro administrador, hecho por un administrador.</summary>
public record RegistroAdministradorDto(string Nombre, string Apellido, string Ci, string Correo, string Contrasena);

public record LoginDto(string Correo, string Contrasena);

/// <summary>Lo que el frontend ve de un usuario: nunca incluye el hash de la contraseña.</summary>
public record UsuarioDto(int Id, string Nombre, string Apellido, string Ci, string Correo, RolUsuario Rol, int? ProductorId)
{
    public static UsuarioDto Desde(Usuario u) => new(u.Id, u.Nombre, u.Apellido, u.Ci, u.Correo, u.Rol, u.ProductorId);
}

public record RespuestaLogin(string Token, DateTime Expira, UsuarioDto Usuario);
