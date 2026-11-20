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

/// <summary>Registro de otro administrador, hecho por un administrador.</summary>
public record RegistroAdministradorDto(string Nombre, string Apellido, string Ci, string Correo, string Contrasena);

public record LoginDto(string Correo, string Contrasena);

/// <summary>Lo que el frontend ve de un usuario: nunca incluye el hash de la contraseña.</summary>
public record UsuarioDto(int Id, string Nombre, string Apellido, string Ci, string Correo, RolUsuario Rol, int? ProductorId)
{
    public static UsuarioDto Desde(Usuario u) => new(u.Id, u.Nombre, u.Apellido, u.Ci, u.Correo, u.Rol, u.ProductorId);
}

/// <summary>Cuentas que se crean al iniciar la API (sección "CuentasIniciales" de appsettings), para que existan los roles internos.</summary>
public class CuentaInicial
{
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string Ci { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Contrasena { get; set; } = "";
    public RolUsuario Rol { get; set; }
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
    public const string ProductorId = "productorId";
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
            Subject = new ClaimsIdentity(Afirmaciones(usuario))
        };

        return new RespuestaLogin(new JsonWebTokenHandler().CreateToken(descriptor), expira, UsuarioDto.Desde(usuario));
    }

    private static IEnumerable<Claim> Afirmaciones(Usuario usuario)
    {
        yield return new Claim(Claims.Id, usuario.Id.ToString());
        yield return new Claim(Claims.Nombre, $"{usuario.Nombre} {usuario.Apellido}");
        yield return new Claim(Claims.Correo, usuario.Correo);
        yield return new Claim(Claims.Rol, usuario.Rol.ToString());

        // El productor lleva su id en el token para que la API filtre sus predios sin consultar nada más (RNF-03).
        if (usuario.ProductorId is int productorId)
            yield return new Claim(Claims.ProductorId, productorId.ToString());
    }
}

/// <summary>
/// CU-01: cuentas de acceso e inicio de sesión. La contraseña se guarda con hash.
/// El registro público de productores está en ServicioProductores, que usa CrearCuenta.
/// </summary>
public partial class ServicioAutenticacion(
    IRepositorio<Usuario> usuarios,
    IPasswordHasher<Usuario> hasher,
    IGeneradorToken generador)
{
    /// <summary>
    /// Valida el correo y la contraseña y crea la cuenta. No la guarda si algún dato falla.
    /// Los datos personales los valida quien llama (ServicioProductores o RegistrarInterno).
    /// </summary>
    public Usuario CrearCuenta(string nombre, string apellido, string ci, string correo, string contrasena, RolUsuario rol, int? productorId = null)
    {
        correo = correo?.Trim().ToLowerInvariant() ?? "";
        contrasena ??= "";

        if (!FormatoCorreo().IsMatch(correo)) throw new DatosInvalidosException("El correo no es válido.", "correo");
        if (contrasena.Length < 8) throw new DatosInvalidosException("La contraseña debe tener al menos 8 caracteres.", "contrasena");
        if (ExisteCorreo(correo)) throw new DatosInvalidosException("Ya existe una cuenta con ese correo.", "correo");

        var usuario = new Usuario
        {
            Nombre = nombre.Trim(),
            Apellido = apellido.Trim(),
            Ci = ci.Trim(),
            Correo = correo,
            Rol = rol,
            ProductorId = productorId
        };
        usuario.ContrasenaHash = hasher.HashPassword(usuario, contrasena);
        return usuarios.Agregar(usuario);
    }

    /// <summary>Comprueba el correo antes de crear otros datos, para no dejar registros a medias.</summary>
    public bool ExisteCorreo(string correo)
    {
        var normalizado = correo?.Trim().ToLowerInvariant() ?? "";
        return usuarios.Listar(u => u.Correo == normalizado).Count > 0;
    }

    /// <summary>Registro del administrador (Sprint 1). Solo lo usa otro administrador.</summary>
    public UsuarioDto RegistrarAdministrador(RegistroAdministradorDto dto)
    {
        var nombre = dto.Nombre?.Trim() ?? "";
        var apellido = dto.Apellido?.Trim() ?? "";
        var ci = dto.Ci?.Trim() ?? "";

        if (nombre.Length == 0) throw new DatosInvalidosException("Ingresa el nombre.", "nombre");
        if (apellido.Length == 0) throw new DatosInvalidosException("Ingresa el apellido.", "apellido");
        if (!FormatoCi().IsMatch(ci)) throw new DatosInvalidosException("El CI debe tener entre 5 y 10 dígitos.", "ci");
        if (usuarios.Listar(u => u.Ci == ci && u.Rol != RolUsuario.Productor).Count > 0)
            throw new DatosInvalidosException("Ya existe un administrador con ese CI.", "ci");

        return UsuarioDto.Desde(CrearCuenta(nombre, apellido, ci, dto.Correo ?? "", dto.Contrasena ?? "", RolUsuario.Administrador));
    }

    /// <summary>Crea las cuentas iniciales que todavía no existen. Se llama una vez al arrancar la API.</summary>
    public int CrearCuentasIniciales(IEnumerable<CuentaInicial> cuentas)
    {
        var creadas = 0;
        foreach (var c in cuentas)
        {
            if (ExisteCorreo(c.Correo)) continue;
            CrearCuenta(c.Nombre, c.Apellido, c.Ci, c.Correo, c.Contrasena, c.Rol);
            creadas++;
        }
        return creadas;
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
