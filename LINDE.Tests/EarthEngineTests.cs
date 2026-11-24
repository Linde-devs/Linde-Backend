using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LINDE.Api.Cobertura;
using Microsoft.Extensions.Options;

namespace LINDE.Tests;

/// <summary>
/// Adapter de Earth Engine sin conexión: las respuestas de Google se simulan con un HttpMessageHandler,
/// así se comprueba qué se envía y cómo se traduce lo que vuelve.
/// </summary>
public class EarthEngineTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 24, 15, 0, 0, TimeSpan.Zero);

    private class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private class TokenFijo : ITokenEarthEngine
    {
        public string Proyecto => "linde-prueba";
        public Task<string> ObtenerAsync(CancellationToken ct = default) => Task.FromResult("token-de-prueba");
    }

    /// <summary>Responde según la función principal de la expresión y guarda cada solicitud.</summary>
    private class EarthEngineFalso(Func<string, (HttpStatusCode, string)> responder) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Solicitud, string Cuerpo)> Recibidas { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var cuerpo = await request.Content!.ReadAsStringAsync(ct);
            lock (Recibidas) Recibidas.Add((request, cuerpo));
            var (estado, json) = responder(cuerpo);
            return new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static (HttpStatusCode, string) Normal(string cuerpo)
    {
        if (cuerpo.Contains("AggregateFeatureCollection.max"))
            return (HttpStatusCode.OK, $$"""{"result": {{new DateTimeOffset(2026, 11, 19, 14, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()}}}""");
        if (cuerpo.Contains("JRC/GFC2020/V3")) return (HttpStatusCode.OK, """{"result": {"Map": 0.62}}""");
        return (HttpStatusCode.OK, """{"result": {"label_mode": 0.48}}""");
    }

    private static EarthEngineAdapter Adaptador(EarthEngineFalso falso) =>
        new(new HttpClient(falso), Options.Create(new OpcionesCobertura { Proveedor = "EarthEngine" }), new TokenFijo(), new RelojFijo(Ahora));

    [Fact]
    public async Task Adapter_PideLasTresConsultasYTraduceLasFraccionesAPorcentajes()
    {
        var falso = new EarthEngineFalso(Normal);

        var r = await Adaptador(falso).AnalizarAsync(Ayudas.Cuadrado(), new DateOnly(2020, 12, 31));

        Assert.Equal(62, r.BosquePorcentajeCorte, 3);
        Assert.Equal(48, r.BosquePorcentajeActual, 3);
        Assert.Equal(new DateOnly(2026, 11, 19), r.FechaImagenActual);
        Assert.Equal(3, falso.Recibidas.Count);
        Assert.All(falso.Recibidas, x =>
        {
            Assert.Equal("https://earthengine.googleapis.com/v1/projects/linde-prueba/value:compute", x.Solicitud.RequestUri!.ToString());
            Assert.Equal("Bearer token-de-prueba", x.Solicitud.Headers.Authorization!.ToString());
        });
    }

    [Fact]
    public async Task Adapter_ArmaElGrafoConElPoligonoYLaVentanaDeFechas()
    {
        var falso = new EarthEngineFalso(Normal);
        await Adaptador(falso).AnalizarAsync(Ayudas.Cuadrado(), new DateOnly(2020, 12, 31));

        var actual = JsonNode.Parse(falso.Recibidas.Single(x => x.Cuerpo.Contains("DYNAMICWORLD") && x.Cuerpo.Contains("Image.eq")).Cuerpo)!;
        var raiz = actual["expression"]!["values"]!["0"]!["functionInvocationValue"]!;
        Assert.Equal("0", actual["expression"]!["result"]!.GetValue<string>());
        Assert.Equal("Image.reduceRegion", raiz["functionName"]!.GetValue<string>());

        var texto = actual.ToJsonString();
        Assert.Contains("\"2026-05-28\"", texto);   // 180 días antes del 24/11/2026
        Assert.Contains("\"2026-11-25\"", texto);   // el filtro termina al día siguiente para incluir hoy
        Assert.Contains("\"label_mode\"", texto);

        var anillo = raiz["arguments"]!["geometry"]!["functionInvocationValue"]!["arguments"]!["coordinates"]!["constantValue"]![0]!.AsArray();
        Assert.Equal(anillo[0]!.ToJsonString(), anillo[^1]!.ToJsonString());
        Assert.Equal(Ayudas.Cuadrado()[0].Lon, anillo[0]![0]!.GetValue<double>());
    }

    [Fact]
    public async Task Adapter_SiFallaLaFechaUsaElUltimoDiaDeLaVentana()
    {
        var falso = new EarthEngineFalso(c => c.Contains("AggregateFeatureCollection.max")
            ? (HttpStatusCode.BadRequest, """{"error": {"message": "Filter.intersects: argumento inválido"}}""")
            : Normal(c));

        var r = await Adaptador(falso).AnalizarAsync(Ayudas.Cuadrado(), new DateOnly(2020, 12, 31));

        Assert.Equal(new DateOnly(2026, 11, 24), r.FechaImagenActual);
        Assert.Equal(62, r.BosquePorcentajeCorte, 3);
    }

    [Fact]
    public async Task Adapter_MuestraElMensajeDeEarthEngineCuandoRechazaLaConsulta()
    {
        var falso = new EarthEngineFalso(_ =>
            (HttpStatusCode.Forbidden, """{"error": {"code": 403, "message": "Caller does not have required permission"}}"""));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Adaptador(falso).AnalizarAsync(Ayudas.Cuadrado(), new DateOnly(2020, 12, 31)));

        Assert.Contains("403", error.Message);
        Assert.Contains("Caller does not have required permission", error.Message);
    }

    [Fact]
    public async Task Adapter_SinImagenesRecientesLoInforma()
    {
        var falso = new EarthEngineFalso(c => c.Contains("Image.eq") ? (HttpStatusCode.OK, """{"result": {"label_mode": null}}""") : Normal(c));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Adaptador(falso).AnalizarAsync(Ayudas.Cuadrado(), new DateOnly(2020, 12, 31)));
        Assert.Contains("180 días", error.Message);
    }

    // ---------- Token de la cuenta de servicio ----------

    private class Fabrica(HttpMessageHandler manejador) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(manejador, disposeHandler: false);
    }

    [Fact]
    public async Task Token_FirmaElJwtConLaLlaveYLoGuardaHastaQueVence()
    {
        using var rsa = RSA.Create(2048);
        var ruta = Path.Combine(Path.GetTempPath(), $"llave-{Guid.NewGuid():N}.json");
        File.WriteAllText(ruta, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "linde-de-la-llave",
            ["private_key_id"] = "abc123",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "linde@linde-de-la-llave.iam.gserviceaccount.com",
            ["token_uri"] = "https://oauth2.googleapis.com/token"
        }));
        try
        {
            var google = new EarthEngineFalso(_ => (HttpStatusCode.OK, """{"access_token": "ya29.prueba", "expires_in": 3599}"""));
            var opciones = Options.Create(new OpcionesCobertura { ArchivoCredenciales = ruta });
            var servicio = new TokenCuentaServicio(new Fabrica(google), opciones, new RelojFijo(Ahora));

            Assert.Equal("linde-de-la-llave", servicio.Proyecto);
            Assert.Equal("ya29.prueba", await servicio.ObtenerAsync());
            Assert.Equal("ya29.prueba", await servicio.ObtenerAsync());
            Assert.Single(google.Recibidas);

            var formulario = google.Recibidas[0].Cuerpo.Split('&').Select(p => p.Split('='))
                .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
            Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", formulario["grant_type"]);

            var partes = formulario["assertion"].Split('.');
            Assert.Equal(3, partes.Length);
            var valida = rsa.VerifyData(Encoding.ASCII.GetBytes($"{partes[0]}.{partes[1]}"), DesdeBase64Url(partes[2]),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Assert.True(valida);

            var contenido = JsonNode.Parse(DesdeBase64Url(partes[1]))!;
            Assert.Equal("linde@linde-de-la-llave.iam.gserviceaccount.com", contenido["iss"]!.GetValue<string>());
            Assert.Equal(TokenCuentaServicio.Alcance, contenido["scope"]!.GetValue<string>());
            Assert.Equal(Ahora.ToUnixTimeSeconds() + 3600, contenido["exp"]!.GetValue<long>());
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task Token_SinLlaveConfiguradaDaUnMensajeClaro()
    {
        var servicio = new TokenCuentaServicio(new Fabrica(new EarthEngineFalso(Normal)), Options.Create(new OpcionesCobertura()));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ObtenerAsync());
        Assert.Contains("Cobertura:ArchivoCredenciales", error.Message);
    }

    private static byte[] DesdeBase64Url(string texto)
    {
        var b = texto.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b.PadRight(b.Length + (4 - b.Length % 4) % 4, '='));
    }
}
