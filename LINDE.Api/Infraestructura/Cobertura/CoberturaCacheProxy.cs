using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using Microsoft.Extensions.Caching.Memory;

namespace LINDE.Api.Cobertura;

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

    /// <summary>Las imágenes también se guardan: pedirlas cuesta cuota de Earth Engine y unos segundos.</summary>
    public async Task<ImagenSatelital?> ImagenAsync(IReadOnlyList<Coordenada> vertices, MomentoImagen momento, CancellationToken ct = default)
    {
        var clave = $"imagen:{Geometrias.Huella(vertices)}:{momento}";
        if (cache.TryGetValue(clave, out ImagenSatelital? guardada) && guardada is not null)
            return guardada;

        var imagen = await real.ImagenAsync(vertices, momento, ct);
        // La de referencia no cambia nunca; la actual se renueva un día después.
        if (imagen is not null)
            cache.Set(clave, imagen, momento == MomentoImagen.Referencia ? duracion : TimeSpan.FromDays(1));
        return imagen;
    }
}
