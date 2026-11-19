using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LINDE.Api.Autenticacion;

public record RegistroDto(string Nombre, string Apellido, string Ci, string Correo, string Contrasena);

public record LoginDto(string Correo, string Contrasena);

/// <summary>Lo que el frontend ve de un usuario: nunca incluye el hash de la contraseña.</summary>
public record UsuarioDto(int Id, string Nombre, string Apellido, string Ci, string Correo, RolUsuario Rol)
{
    public static UsuarioDto Desde(Usuario u) => new(u.Id, u.Nombre, u.Apellido, u.Ci, u.Correo, u.Rol);
}

public record RespuestaLogin(string Token, DateTime Expira, UsuarioDto Usuario);

/// <summary>Configuración del token (sección "Jwt" de appsettings).</summary>
public class OpcionesJwt
{
    public string Emisor { get; set; } = "LINDE.Api";
    public string Audiencia { get; set; } = "LINDE.Frontend";

    /// <summary>Clave secreta para firmar los tokens. Mínimo 32 caracteres. En producción, fuera del repositorio.</summary>
    public string Clave { get; set; } = "";

    public int MinutosExpiracion { get; set; } = 480;

    public SymmetricSecurityKey ClaveFirma()
    {
        if (Encoding.UTF8.GetByteCount(Clave) < 32)
            throw new InvalidOperationException(
                "Falta la clave del token (Jwt:Clave) o tiene menos de 32 caracteres. Configúrala en appsettings.Development.json.");

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Clave));
    }
}

/// <summary>Nombres cortos de las afirmaciones (claims) del token.</summary>
public static class Claims
{
    public const string Id = JwtRegisteredClaimNames.Sub;
    public const string Nombre = "name";
    public const string Correo = "email";
    public const string Rol = "role";
}

public interface IGeneradorToken
{
    RespuestaLogin Generar(Usuario usuario);
}

public class GeneradorTokenJwt(IOptions<OpcionesJwt> opciones) : IGeneradorToken
{
    public RespuestaLogin Generar(Usuario usuario)
    {
        var config = opciones.Value;
        var expira = DateTime.UtcNow.AddMinutes(config.MinutosExpiracion);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config.Emisor,
            Audience = config.Audiencia,
            Expires = expira,
            SigningCredentials = new SigningCredentials(config.ClaveFirma(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(Claims.Id, usuario.Id.ToString()),
                new Claim(Claims.Nombre, $"{usuario.Nombre} {usuario.Apellido}"),
                new Claim(Claims.Correo, usuario.Correo),
                new Claim(Claims.Rol, usuario.Rol.ToString())
            })
        };

        return new RespuestaLogin(new JsonWebTokenHandler().CreateToken(descriptor), expira, UsuarioDto.Desde(usuario));
    }
}

/// <summary>
/// CU-01: registro e inicio de sesión. Reemplaza la lista que estaba en AuthContext.jsx:
/// ahora los usuarios viven en el backend y la contraseña se guarda con hash.
/// </summary>
public partial class ServicioAutenticacion(
    IRepositorio<Usuario> usuarios,
    IPasswordHasher<Usuario> hasher,
    IGeneradorToken generador)
{
    public UsuarioDto Registrar(RegistroDto dto)
    {
        var nombre = dto.Nombre?.Trim() ?? "";
        var apellido = dto.Apellido?.Trim() ?? "";
        var ci = dto.Ci?.Trim() ?? "";
        var correo = dto.Correo?.Trim().ToLowerInvariant() ?? "";
        var contrasena = dto.Contrasena ?? "";

        if (nombre.Length == 0) throw new DatosInvalidosException("Ingresa tu nombre.", "nombre");
        if (apellido.Length == 0) throw new DatosInvalidosException("Ingresa tu apellido.", "apellido");
        if (!FormatoCi().IsMatch(ci)) throw new DatosInvalidosException("El CI debe tener entre 5 y 10 dígitos.", "ci");
        if (!FormatoCorreo().IsMatch(correo)) throw new DatosInvalidosException("El correo no es válido.", "correo");
        if (contrasena.Length < 8) throw new DatosInvalidosException("La contraseña debe tener al menos 8 caracteres.", "contrasena");

        if (usuarios.Listar(u => u.Correo == correo).Count > 0)
            throw new DatosInvalidosException("Ya existe una cuenta con ese correo.", "correo");
        if (usuarios.Listar(u => u.Ci == ci).Count > 0)
            throw new DatosInvalidosException("Ya existe una cuenta con ese CI.", "ci");

        var usuario = new Usuario
        {
            Nombre = nombre,
            Apellido = apellido,
            Ci = ci,
            Correo = correo,
            // Por ahora el registro público crea administradores; más adelante será solo para productores.
            Rol = RolUsuario.Administrador
        };
        usuario.ContrasenaHash = hasher.HashPassword(usuario, contrasena);

        return UsuarioDto.Desde(usuarios.Agregar(usuario));
    }

    public RespuestaLogin IniciarSesion(LoginDto dto)
    {
        var correo = dto.Correo?.Trim().ToLowerInvariant() ?? "";
        var usuario = usuarios.Listar(u => u.Correo == correo).FirstOrDefault();

        // El mismo mensaje para correo inexistente y contraseña incorrecta: no revela qué correos existen.
        if (usuario is null ||
            hasher.VerifyHashedPassword(usuario, usuario.ContrasenaHash, dto.Contrasena ?? "") == PasswordVerificationResult.Failed)
            throw new CredencialesInvalidasException();

        return generador.Generar(usuario);
    }

    public UsuarioDto Obtener(int id) =>
        usuarios.Obtener(id) is { } u
            ? UsuarioDto.Desde(u)
            : throw new RecursoNoEncontradoException("La cuenta ya no existe. Inicia sesión de nuevo.");

    [GeneratedRegex(@"^\d{5,10}$")]
    private static partial Regex FormatoCi();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex FormatoCorreo();
}

/// <summary>Correo o contraseña incorrectos. Responde 401.</summary>
public class CredencialesInvalidasException() : Exception("Correo o contraseña incorrectos.");
