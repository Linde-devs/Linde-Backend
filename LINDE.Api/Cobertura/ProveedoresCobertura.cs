using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LINDE.Api.Cobertura;

/// <summary>Resultado que entiende LINDE: porcentaje de bosque en la fecha de corte y en la imagen más reciente.</summary>
public record ResultadoCobertura(
    double BosquePorcentajeCorte,
    double BosquePorcentajeActual,
    DateOnly FechaImagenActual,
    string Fuente);

/// <summary>Interfaz propia de LINDE para el análisis de cobertura. Ninguna validación depende de Earth Engine.</summary>
public interface IProveedorCobertura
{
    Task<ResultadoCobertura> AnalizarAsync(IReadOnlyList<Coordenada> vertices, DateOnly fechaCorte, CancellationToken ct = default);
}

public class OpcionesCobertura
{
    /// <summary>"Simulado" para desarrollo y pruebas; "EarthEngine" para usar el adaptador real.</summary>
    public string Proveedor { get; set; } = "Simulado";

    /// <summary>
    /// Ruta del archivo JSON con la llave de la cuenta de servicio. Va en dotnet user-secrets, nunca en el repositorio,
    /// y el archivo se guarda fuera de la carpeta del proyecto.
    /// </summary>
    public string ArchivoCredenciales { get; set; } = "";

    /// <summary>Proyecto de Google Cloud registrado en Earth Engine. Si queda vacío se usa el project_id de la llave.</summary>
    public string ProyectoGcp { get; set; } = "";

    public string UrlApi { get; set; } = "https://earthengine.googleapis.com/v1";

    /// <summary>Mapa de bosque del JRC al 31/12/2020, la referencia de la EUDR (banda Map: 1 = bosque).</summary>
    public string ColeccionReferencia { get; set; } = "JRC/GFC2020/V3";
    public string BandaReferencia { get; set; } = "Map";

    /// <summary>Dynamic World: cobertura del suelo cada 5 días con Sentinel-2 (banda label: 1 = árboles).</summary>
    public string ColeccionActual { get; set; } = "GOOGLE/DYNAMICWORLD/V1";

    /// <summary>Días hacia atrás que se juntan para la cobertura actual (la moda quita nubes y días sueltos).</summary>
    public int DiasVentanaActual { get; set; } = 180;

    public int DiasCache { get; set; } = 7;
    public int RetardoSimuladoMs { get; set; } = 800;
}

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
public class EarthEngineAdapter(HttpClient http, IOptions<OpcionesCobertura> opciones, ITokenEarthEngine token, TimeProvider? reloj = null)
    : IProveedorCobertura
{
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
        var tareaFecha = CalcularAsync(fecha, ct);

        var bosqueCorte = PrimerNumero(await tareaReferencia) ?? 0;
        var bosqueActual = PrimerNumero(await tareaActual)
                           ?? throw new InvalidOperationException(
                               $"Earth Engine no encontró imágenes despejadas del predio en los últimos {c.DiasVentanaActual} días.");

        var fechaImagen = fin;
        try
        {
            if (PrimerNumero(await tareaFecha) is { } milisegundos)
                fechaImagen = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds((long)milisegundos).UtcDateTime);
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException)
        {
            // La fecha es informativa: si esa consulta falla, se usa el último día de la ventana.
        }

        var nota = fechaCorte == new DateOnly(2020, 12, 31) ? "" : " El mapa de referencia es del 31/12/2020.";
        return new ResultadoCobertura(bosqueCorte * 100, bosqueActual * 100, fechaImagen,
            $"Earth Engine (bosque del JRC 2020 y árboles de Dynamic World de los últimos {c.DiasVentanaActual} días).{nota}");
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

/// <summary>
/// Proveedor de prueba: no se conecta a Earth Engine y devuelve valores fijos para cada geometría.
/// Sirve para desarrollar y probar la validación mientras no haya credenciales.
/// </summary>
public class CoberturaSimulada(IOptions<OpcionesCobertura> opciones) : IProveedorCobertura
{
    public async Task<ResultadoCobertura> AnalizarAsync(IReadOnlyList<Coordenada> vertices, DateOnly fechaCorte, CancellationToken ct = default)
    {
        if (opciones.Value.RetardoSimuladoMs > 0)
            await Task.Delay(opciones.Value.RetardoSimuladoMs, ct);

        var huella = Geometrias.Huella(vertices);
        var semilla = Convert.ToInt32(huella[..6], 16);
        var bosqueCorte = 20 + semilla % 60;
        var perdida = semilla / 60 % 16;

        return new ResultadoCobertura(bosqueCorte, bosqueCorte - perdida,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), "Simulación (sin conexión a Earth Engine)");
    }
}

/// <summary>
/// Patrón Proxy: tiene la misma interfaz que el adaptador y se coloca delante de él.
/// Si ya existe un análisis para la misma geometría y fecha de corte, lo devuelve sin llamar al servicio.
/// Mover un solo vértice cambia la huella y obliga a un análisis nuevo.
/// A diferencia del decorador de auditoría, el proxy decide si delega o no.
/// </summary>
public class CoberturaCacheProxy(IProveedorCobertura real, IMemoryCache cache, TimeSpan duracion) : IProveedorCobertura
{
    public async Task<ResultadoCobertura> AnalizarAsync(IReadOnlyList<Coordenada> vertices, DateOnly fechaCorte, CancellationToken ct = default)
    {
        var clave = $"cobertura:{Geometrias.Huella(vertices)}:{fechaCorte:yyyy-MM-dd}";
        if (cache.TryGetValue(clave, out ResultadoCobertura? guardado) && guardado is not null)
            return guardado;

        var resultado = await real.AnalizarAsync(vertices, fechaCorte, ct);
        cache.Set(clave, resultado, duracion);
        return resultado;
    }
}
