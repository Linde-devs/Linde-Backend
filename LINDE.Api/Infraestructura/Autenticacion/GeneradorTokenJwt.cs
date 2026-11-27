using System.Security.Claims;
using LINDE.Api.Dominio;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LINDE.Api.Autenticacion;

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
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(config.MinutosExpiracion);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config.Emisor,
            Audience = config.Audiencia,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            SigningCredentials = new SigningCredentials(config.ClaveFirma(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(Afirmaciones(usuario))
        };

        return new RespuestaLogin(new JsonWebTokenHandler().CreateToken(descriptor), expira, UsuarioDto.Desde(usuario));
    }

    private static IEnumerable<Claim> Afirmaciones(Usuario usuario)
    {
        yield return new Claim(Claims.Id, usuario.Id.ToString());
        // Identificador único de cada token emitido.
        yield return new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString());
        yield return new Claim(Claims.Nombre, $"{usuario.Nombre} {usuario.Apellido}");
        yield return new Claim(Claims.Correo, usuario.Correo);
        yield return new Claim(Claims.Rol, usuario.Rol.ToString());

        // El productor lleva su id en el token para que la API filtre sus predios sin consultar nada más (RNF-03).
        if (usuario.ProductorId is int productorId)
            yield return new Claim(Claims.ProductorId, productorId.ToString());
    }
}
