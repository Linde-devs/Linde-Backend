using LINDE.Api.Autenticacion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LINDE.Tests;

public class AutenticacionTests
{
    private static readonly OpcionesJwt Opciones = new() { Clave = "clave-de-prueba-con-mas-de-32-caracteres-123" };

    private static (ServicioAutenticacion Servicio, RepositorioMemoria<Usuario> Usuarios) Crear()
    {
        var usuarios = new RepositorioMemoria<Usuario>();
        var servicio = new ServicioAutenticacion(usuarios, new PasswordHasher<Usuario>(), new GeneradorTokenJwt(Options.Create(Opciones)));
        return (servicio, usuarios);
    }

    private static RegistroDto Registro(string correo = "admin@linde.bo", string ci = "7845123") =>
        new("Mauricio", "Urioste", ci, correo, "linde2026");

    [Fact]
    public void Registro_GuardaLaContrasenaConHashYNuncaEnTextoPlano()
    {
        var (servicio, usuarios) = Crear();

        servicio.Registrar(Registro());

        var guardado = Assert.Single(usuarios.Listar());
        Assert.False(guardado.ContrasenaHash.Contains("linde2026"));
        Assert.True(guardado.ContrasenaHash.Length > 40);
    }

    [Fact]
    public void Registro_RechazaCorreoRepetidoIndicandoElCampo()
    {
        var (servicio, _) = Crear();
        servicio.Registrar(Registro());

        var error = Assert.Throws<DatosInvalidosException>(() => servicio.Registrar(Registro(correo: "ADMIN@linde.bo", ci: "1111111")));

        Assert.Equal("correo", error.Campo);
    }

    [Fact]
    public void Login_ConContrasenaIncorrectaFalla()
    {
        var (servicio, _) = Crear();
        servicio.Registrar(Registro());

        Assert.Throws<CredencialesInvalidasException>(() => servicio.IniciarSesion(new LoginDto("admin@linde.bo", "otra-clave")));
        Assert.Throws<CredencialesInvalidasException>(() => servicio.IniciarSesion(new LoginDto("nadie@linde.bo", "linde2026")));
    }

    [Fact]
    public async Task Login_DevuelveUnTokenFirmadoConElRol()
    {
        var (servicio, _) = Crear();
        servicio.Registrar(Registro());

        var respuesta = servicio.IniciarSesion(new LoginDto("Admin@Linde.bo", "linde2026"));

        var validacion = await new JsonWebTokenHandler().ValidateTokenAsync(respuesta.Token, new TokenValidationParameters
        {
            ValidIssuer = Opciones.Emisor,
            ValidAudience = Opciones.Audiencia,
            IssuerSigningKey = Opciones.ClaveFirma()
        });

        Assert.True(validacion.IsValid);
        Assert.Equal("Administrador", validacion.Claims[Claims.Rol]);
        Assert.Equal("admin@linde.bo", validacion.Claims[Claims.Correo]);
        Assert.Equal(RolUsuario.Administrador, respuesta.Usuario.Rol);
    }
}
