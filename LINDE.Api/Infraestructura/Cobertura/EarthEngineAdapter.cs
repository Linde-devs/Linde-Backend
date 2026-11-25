using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LINDE.Api.Dominio;
using Microsoft.Extensions.Options;

namespace LINDE.Api.Cobertura;

/// <summary>
/// Patrón Adapter: implementa la interfaz de LINDE y por dentro habla con Earth Engine en su propio formato.
/// Earth Engine no tiene librería oficial para .NET, así que se usa su API REST (value:compute) con un grafo de
/// expresiones en JSON, el mismo formato al que traduce sus llamadas la librería de Python.
/// Se piden tres valores para el polígono del predio:
/// 1. Fracción de bosque en el mapa del JRC 2020 (referencia de la fecha de corte).
/// 2. Fracción de árboles en la moda de Dynamic World de los últimos días (cobertura actual).
/// 3. Fecha de la imagen más reciente sobre el predio (si falla, se informa el último día de la ventana).
/// Las fracciones (0 a 1) se traducen a porcentajes, que es lo que entiende ValidadorCobertura.
/// </summary>
public class EarthEngineAdapter(HttpClient http, IOptions<OpcionesCobertura> opciones, ITokenEarthEngine token,
    TimeProvider? reloj = null, ILogger<EarthEngineAdapter>? registro = null)
    : IProveedorCobertura
{
    /// <summary>La imagen de referencia junta la estación seca de 2020, la más despejada antes de la fecha de corte.</summary>
    public static readonly DateOnly InicioReferencia = new(2020, 5, 1), FinReferencia = new(2020, 12, 31);

    private const int EscalaMetros = 10;
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<ResultadoCobertura> AnalizarAsync(IReadOnlyList<Coordenada> vertices, DateOnly fechaCorte, CancellationToken ct = default)
    {
        var c = opciones.Value;
        var fin = DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime);
        var inicio = fin.AddDays(-c.DiasVentanaActual);
        var geometria = ExpresionesEarthEngine.Poligono(vertices);
        var recientes = ExpresionesEarthEngine.ColeccionEntre(c.ColeccionActual, inicio, fin.AddDays(1));

        var referencia = ExpresionesEarthEngine.FraccionReferencia(c.ColeccionReferencia, c.BandaReferencia, geometria, EscalaMetros);
        var actual = ExpresionesEarthEngine.FraccionArbolesActual(recientes, geometria, EscalaMetros);
        var fecha = ExpresionesEarthEngine.FechaMasReciente(recientes, geometria);

        var tareaReferencia = CalcularAsync(referencia, ct);
        var tareaActual = CalcularAsync(actual, ct);
        var tareaFecha = FechaMasRecienteAsync(recientes, geometria, ct);

        var bosqueCorte = PrimerNumero(await tareaReferencia) ?? 0;
        var bosqueActual = PrimerNumero(await tareaActual)
                           ?? throw new InvalidOperationException(
                               $"Earth Engine no encontró imágenes despejadas del predio en los últimos {c.DiasVentanaActual} días.");

        // La fecha es informativa: si no se pudo leer, se usa el último día de la ventana y se avisa por qué.
        var (fechaLeida, avisoFecha) = await tareaFecha;
        var fechaImagen = fechaLeida ?? fin;

        var nota = fechaCorte == new DateOnly(2020, 12, 31) ? "" : " El mapa de referencia es del 31/12/2020.";
        return new ResultadoCobertura(bosqueCorte * 100, bosqueActual * 100, fechaImagen,
            $"Earth Engine (bosque del JRC 2020 y árboles de Dynamic World de los últimos {c.DiasVentanaActual} días).{nota}",
            avisoFecha);
    }

    /// <summary>
    /// Fecha de la imagen más reciente que cubre el predio. Se prueba primero con la geometría sola en el filtro y,
    /// si el servidor no la acepta, envuelta en un Feature, que es como la manda la librería de JavaScript.
    /// </summary>
    private async Task<(DateOnly? Fecha, string? Aviso)> FechaMasRecienteAsync(JsonObject recientes, JsonObject geometria, CancellationToken ct)
    {
        var errores = new List<string>();
        foreach (var conFeature in new[] { false, true })
        {
            try
            {
                var valor = await CalcularAsync(ExpresionesEarthEngine.FechaMasReciente(recientes, geometria, conFeature), ct);
                if (PrimerNumero(valor) is { } milisegundos)
                    return (DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds((long)milisegundos).UtcDateTime), null);
                errores.Add("no hay imágenes sobre el predio");
                break;
            }
            catch (Exception e) when (e is InvalidOperationException or HttpRequestException)
            {
                errores.Add(e.Message);
            }
        }

        var aviso = $"No se pudo leer la fecha de la imagen más reciente ({string.Join(" / ", errores)}); se muestra el último día del período.";
        registro?.LogWarning("Earth Engine: {Aviso}", aviso);
        return (null, aviso);
    }

    /// <summary>
    /// RF-08. Composición en color natural de Sentinel-2 (mediana de las imágenes con poca nubosidad) del entorno del
    /// predio, pedida a image:computePixels como JPEG. La de referencia es de mayo a diciembre de 2020; la actual, de
    /// los últimos días configurados.
    /// </summary>
    public async Task<ImagenSatelital?> ImagenAsync(IReadOnlyList<Coordenada> vertices, MomentoImagen momento, CancellationToken ct = default)
    {
        var c = opciones.Value;
        var hoy = DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime);
        var (inicio, fin) = momento == MomentoImagen.Referencia
            ? (InicioReferencia, FinReferencia)
            : (hoy.AddDays(-c.DiasVentanaActual), hoy);

        var marco = MarcoImagen.Calcular(vertices);
        var expresion = ExpresionesEarthEngine.ColorNatural(c.ColeccionImagenes, inicio, fin.AddDays(1), c.NubosidadMaximaPorcentaje);
        var cuerpo = new JsonObject
        {
            ["expression"] = ExpresionesEarthEngine.Solicitud(expresion)["expression"]!.DeepClone(),
            ["fileFormat"] = "JPEG",
            ["grid"] = new JsonObject
            {
                ["dimensions"] = new JsonObject { ["width"] = marco.Ancho, ["height"] = marco.Alto },
                ["affineTransform"] = new JsonObject
                {
                    ["scaleX"] = (marco.Este - marco.Oeste) / marco.Ancho, ["shearX"] = 0, ["translateX"] = marco.Oeste,
                    ["shearY"] = 0, ["scaleY"] = -(marco.Norte - marco.Sur) / marco.Alto, ["translateY"] = marco.Norte
                },
                ["crsCode"] = "EPSG:4326"
            }
        };

        var url = $"{c.UrlApi.TrimEnd('/')}/projects/{token.Proyecto}/image:computePixels";
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json")
        };
        mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await token.ObtenerAsync(ct));

        using var respuesta = await http.SendAsync(mensaje, ct);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync(ct);
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Earth Engine respondió {(int)respuesta.StatusCode}: {MensajeError(Encoding.UTF8.GetString(bytes))}");

        var periodo = momento == MomentoImagen.Referencia
            ? "Mayo a diciembre de 2020"
            : $"{inicio:dd/MM/yyyy} a {fin:dd/MM/yyyy}";
        return new ImagenSatelital(bytes, "image/jpeg", periodo, [marco.Oeste, marco.Sur, marco.Este, marco.Norte],
            $"Sentinel-2, mediana de las imágenes con nubosidad menor al {c.NubosidadMaximaPorcentaje} %");
    }

    private async Task<JsonNode?> CalcularAsync(JsonObject expresion, CancellationToken ct)
    {
        var c = opciones.Value;
        var url = $"{c.UrlApi.TrimEnd('/')}/projects/{token.Proyecto}/value:compute";
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(ExpresionesEarthEngine.Solicitud(expresion).ToJsonString(), Encoding.UTF8, "application/json")
        };
        mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await token.ObtenerAsync(ct));

        using var respuesta = await http.SendAsync(mensaje, ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Earth Engine respondió {(int)respuesta.StatusCode}: {MensajeError(cuerpo)}");
        return JsonNode.Parse(cuerpo)?["result"];
    }

    /// <summary>reduceRegion devuelve un diccionario con una sola banda ({"Map": 0.62}); la fecha llega como número.</summary>
    private static double? PrimerNumero(JsonNode? valor) => valor switch
    {
        JsonValue v when v.TryGetValue<double>(out var n) => n,
        JsonObject o => o.Select(p => PrimerNumero(p.Value)).FirstOrDefault(n => n is not null),
        _ => null
    };

    private static string MensajeError(string cuerpo)
    {
        try
        {
            var mensaje = JsonNode.Parse(cuerpo)?["error"]?["message"]?.GetValue<string>();
            if (mensaje is not null) return mensaje.Length > 300 ? mensaje[..300] : mensaje;
        }
        catch (JsonException) { }
        return cuerpo.Length > 200 ? cuerpo[..200] : cuerpo;
    }
}
