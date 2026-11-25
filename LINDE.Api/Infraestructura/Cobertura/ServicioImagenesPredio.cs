using LINDE.Api.Autenticacion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Cobertura;

/// <summary>Una imagen lista para la pantalla: el JPEG como data URL, sus fechas y el recuadro que cubre.</summary>
public record ImagenVista(string DatosUrl, string Periodo, double[] Limites, string Fuente);

/// <summary>Las dos imágenes de RF-08. Si no hay imágenes, Disponible es false y Mensaje dice por qué.</summary>
public record ImagenesPredio(bool Disponible, string? Mensaje, ImagenVista? Referencia, ImagenVista? Actual);

/// <summary>
/// RF-08. Imágenes satelitales del predio antes de la fecha de corte (2020) y ahora, para que el técnico compare a
/// simple vista lo que la capa de cobertura resume en porcentajes. Usa el mismo IProveedorCobertura que la
/// validación, así que pasa por el Proxy de caché y por el Adapter de Earth Engine.
/// </summary>
public class ServicioImagenesPredio(
    IRepositorio<Predio> predios,
    IRepositorio<Poligono> poligonos,
    ControlAcceso acceso,
    IProveedorCobertura proveedor)
{
    public async Task<ImagenesPredio> ObtenerAsync(int predioId, CancellationToken ct = default)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        acceso.VerificarPredio(predio);
        var poligono = poligonos.Listar(p => p.PredioId == predioId && p.Vigente).FirstOrDefault()
                       ?? throw new DatosInvalidosException("El predio todavía no tiene polígono ni punto registrado.");

        try
        {
            var referencia = proveedor.ImagenAsync(poligono.Vertices, MomentoImagen.Referencia, ct);
            var actual = proveedor.ImagenAsync(poligono.Vertices, MomentoImagen.Actual, ct);
            var (r, a) = (await referencia, await actual);
            if (r is null || a is null)
                return new(false, "Las imágenes satelitales necesitan la conexión con Google Earth Engine (Cobertura:Proveedor = EarthEngine).", null, null);
            return new(true, null, Vista(r), Vista(a));
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return new(false, $"No se pudieron obtener las imágenes: {e.Message}", null, null);
        }
    }

    private static ImagenVista Vista(ImagenSatelital i) =>
        new($"data:{i.Tipo};base64,{Convert.ToBase64String(i.Contenido)}", i.Periodo, i.Limites, i.Fuente);
}
