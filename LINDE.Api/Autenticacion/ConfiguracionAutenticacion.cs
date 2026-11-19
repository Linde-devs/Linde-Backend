using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace LINDE.Api.Autenticacion;

/// <summary>Nombres de los roles para usar en [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Administrador = nameof(RolUsuario.Administrador);
    public const string Verificador = nameof(RolUsuario.Verificador);
    public const string VerificadorOAdministrador = Verificador + "," + Administrador;
}

public static class ConfiguracionAutenticacion
{
    /// <summary>
    /// Autenticación con token JWT. Por defecto todo endpoint exige sesión iniciada;
    /// los públicos (registro y login) se marcan con [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AgregarAutenticacionJwt(this IServiceCollection services, IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection("Jwt");
        services.Configure<OpcionesJwt>(seccion);
        var opciones = seccion.Get<OpcionesJwt>() ?? new OpcionesJwt();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = opciones.Emisor,
                    ValidateAudience = true,
                    ValidAudience = opciones.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = opciones.ClaveFirma(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = Claims.Nombre,
                    RoleClaimType = Claims.Rol
                };
            });

        services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddSingleton<IGeneradorToken, GeneradorTokenJwt>();
        services.AddScoped<ServicioAutenticacion>();

        return services;
    }
}
