using System.Text.RegularExpressions;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using Microsoft.AspNetCore.Identity;

namespace LINDE.Api.Autenticacion;

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
