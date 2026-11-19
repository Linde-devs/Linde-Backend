using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
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

    /// <summary>Endpoint que ejecuta el análisis en Earth Engine (API REST o microservicio con la librería de Python).</summary>
    public string UrlServicio { get; set; } = "";

    public string? Token { get; set; }
    public string ColeccionReferencia { get; set; } = "JRC/GFC2020/V2";
    public string ColeccionActual { get; set; } = "COPERNICUS/S2_SR_HARMONIZED";
    public int DiasCache { get; set; } = 7;
    public int RetardoSimuladoMs { get; set; } = 800;
}

/// <summary>
/// Patrón Adapter: implementa la interfaz de LINDE y por dentro habla con Earth Engine en su propio formato.
/// Earth Engine no tiene librería oficial para .NET, así que se usa HTTP.
/// Convierte el polígono a GeoJSON, envía la solicitud y traduce la respuesta (fracciones 0 a 1) a porcentajes.
/// </summary>
public class EarthEngineAdapter(HttpClient http, IOptions<OpcionesCobertura> opciones) : IProveedorCobertura
{
    public async Task<ResultadoCobertura> AnalizarAsync(IReadOnlyList<Coordenada> vertices, DateOnly fechaCorte, CancellationToken ct = default)
    {
        var config = opciones.Value;
        if (string.IsNullOrWhiteSpace(config.UrlServicio))
            throw new InvalidOperationException("No está configurada la URL del servicio de Earth Engine (Cobertura:UrlServicio).");

        var solicitud = new SolicitudEarthEngine(
            new GeometriaGeoJson("Polygon", new[] { vertices.Select(v => new[] { v.Lon, v.Lat }).ToArray() }),
            fechaCorte.ToString("yyyy-MM-dd"),
            config.ColeccionReferencia,
            config.ColeccionActual);

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, config.UrlServicio)
        {
            Content = JsonContent.Create(solicitud)
        };
        if (!string.IsNullOrWhiteSpace(config.Token))
            mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);

        using var respuesta = await http.SendAsync(mensaje, ct);
        respuesta.EnsureSuccessStatusCode();

        var datos = await respuesta.Content.ReadFromJsonAsync<RespuestaEarthEngine>(cancellationToken: ct)
                    ?? throw new InvalidOperationException("Earth Engine devolvió una respuesta vacía.");

        return new ResultadoCobertura(
            datos.FraccionBosqueReferencia * 100,
            datos.FraccionBosqueActual * 100,
            DateOnly.Parse(datos.FechaImagen),
            $"Earth Engine ({config.ColeccionReferencia} y {config.ColeccionActual})");
    }

    private record GeometriaGeoJson(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("coordinates")] double[][][] Coordinates);

    private record SolicitudEarthEngine(
        [property: JsonPropertyName("geometry")] GeometriaGeoJson Geometry,
        [property: JsonPropertyName("cutoffDate")] string CutoffDate,
        [property: JsonPropertyName("referenceCollection")] string ReferenceCollection,
        [property: JsonPropertyName("currentCollection")] string CurrentCollection);

    private record RespuestaEarthEngine(
        [property: JsonPropertyName("forestFractionReference")] double FraccionBosqueReferencia,
        [property: JsonPropertyName("forestFractionCurrent")] double FraccionBosqueActual,
        [property: JsonPropertyName("imageDate")] string FechaImagen);
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
