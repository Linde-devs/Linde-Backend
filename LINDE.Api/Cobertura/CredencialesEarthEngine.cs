using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LINDE.Api.Cobertura;

/// <summary>Entrega un token de acceso vigente para llamar a la API REST de Earth Engine.</summary>
public interface ITokenEarthEngine
{
    /// <summary>Proyecto de Google Cloud registrado en Earth Engine (el de la configuración o el de la llave).</summary>
    string Proyecto { get; }

    Task<string> ObtenerAsync(CancellationToken ct = default);
}

/// <summary>
/// Token con una cuenta de servicio de Google (flujo OAuth 2.0 "JWT bearer"). La llave JSON se lee de un archivo que
/// queda fuera del repositorio; su ruta se guarda con dotnet user-secrets (Cobertura:ArchivoCredenciales).
/// Se arma un JWT firmado con la llave privada (RS256), se cambia por un token de acceso y se guarda hasta un minuto
/// antes de que venza. No hace falta ninguna librería de Google: solo System.Security.Cryptography.
/// </summary>
public class TokenCuentaServicio(IHttpClientFactory fabrica, IOptions<OpcionesCobertura> opciones, TimeProvider? reloj = null)
    : ITokenEarthEngine
{
    public const string Alcance = "https://www.googleapis.com/auth/earthengine";

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private LlaveCuentaServicio? _llave;
    private string? _token;
    private DateTimeOffset _vence;

    public string Proyecto
    {
        get
        {
            var configurado = opciones.Value.ProyectoGcp;
            return string.IsNullOrWhiteSpace(configurado) ? Llave().ProjectId : configurado.Trim();
        }
    }

    public async Task<string> ObtenerAsync(CancellationToken ct = default)
    {
        if (_token is not null && _reloj.GetUtcNow() < _vence) return _token;

        await _candado.WaitAsync(ct);
        try
        {
            if (_token is not null && _reloj.GetUtcNow() < _vence) return _token;

            var llave = Llave();
            var ahora = _reloj.GetUtcNow();
            var jwt = ArmarJwt(llave, ahora);

            using var http = fabrica.CreateClient(nameof(TokenCuentaServicio));
            using var respuesta = await http.PostAsync(llave.TokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = jwt
            }), ct);

            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            if (!respuesta.IsSuccessStatusCode)
                throw new InvalidOperationException($"Google rechazó la cuenta de servicio ({(int)respuesta.StatusCode}): {MensajeError(cuerpo)}");

            var token = JsonSerializer.Deserialize<RespuestaToken>(cuerpo)
                        ?? throw new InvalidOperationException("Google devolvió un token vacío.");
            _token = token.AccessToken;
            _vence = ahora.AddSeconds(Math.Max(60, token.ExpiresIn) - 60);
            return _token;
        }
        finally
        {
            _candado.Release();
        }
    }

    /// <summary>JWT firmado: cabecera y contenido en Base64URL, firmados con RSA SHA-256.</summary>
    public static string ArmarJwt(LlaveCuentaServicio llave, DateTimeOffset ahora)
    {
        var cabecera = new Dictionary<string, string> { ["alg"] = "RS256", ["typ"] = "JWT", ["kid"] = llave.PrivateKeyId };
        var contenido = new Dictionary<string, object>
        {
            ["iss"] = llave.ClientEmail,
            ["scope"] = Alcance,
            ["aud"] = llave.TokenUri,
            ["iat"] = ahora.ToUnixTimeSeconds(),
            ["exp"] = ahora.AddHours(1).ToUnixTimeSeconds()
        };
        var sinFirma = $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(cabecera))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(contenido))}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(llave.PrivateKey);
        var firma = rsa.SignData(Encoding.ASCII.GetBytes(sinFirma), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{sinFirma}.{Base64Url(firma)}";
    }

    public static string Base64Url(byte[] datos) => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private LlaveCuentaServicio Llave()
    {
        if (_llave is not null) return _llave;

        var ruta = opciones.Value.ArchivoCredenciales;
        if (string.IsNullOrWhiteSpace(ruta))
            throw new InvalidOperationException("Falta la ruta de la llave de Earth Engine (Cobertura:ArchivoCredenciales en user-secrets).");
        if (!File.Exists(ruta))
            throw new InvalidOperationException("No se encontró el archivo de la llave de Earth Engine en la ruta configurada.");

        LlaveCuentaServicio? llave;
        try
        {
            llave = JsonSerializer.Deserialize<LlaveCuentaServicio>(File.ReadAllText(ruta));
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("El archivo de la llave de Earth Engine no es un JSON válido.");
        }

        if (llave is null || llave.Type != "service_account" || string.IsNullOrWhiteSpace(llave.PrivateKey) ||
            string.IsNullOrWhiteSpace(llave.ClientEmail))
            throw new InvalidOperationException("El archivo no es la llave JSON de una cuenta de servicio de Google.");
        return _llave = llave;
    }

    private static string MensajeError(string cuerpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            var raiz = doc.RootElement;
            if (raiz.TryGetProperty("error_description", out var d)) return d.GetString() ?? cuerpo;
            if (raiz.TryGetProperty("error", out var e)) return e.ToString();
        }
        catch (JsonException) { }
        return cuerpo.Length > 200 ? cuerpo[..200] : cuerpo;
    }

    private record RespuestaToken(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>Campos que se usan de la llave JSON que descarga Google Cloud para una cuenta de servicio.</summary>
public record LlaveCuentaServicio(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("project_id")] string ProjectId,
    [property: JsonPropertyName("private_key_id")] string PrivateKeyId,
    [property: JsonPropertyName("private_key")] string PrivateKey,
    [property: JsonPropertyName("client_email")] string ClientEmail,
    [property: JsonPropertyName("token_uri")] string TokenUri);
