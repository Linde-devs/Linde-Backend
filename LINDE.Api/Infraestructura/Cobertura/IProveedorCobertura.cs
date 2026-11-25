using LINDE.Api.Dominio;

namespace LINDE.Api.Cobertura;

/// <summary>
/// Resultado que entiende LINDE: porcentaje de bosque en la fecha de corte y en la imagen más reciente.
/// Aviso: algo secundario que no se pudo obtener (por ejemplo, la fecha exacta de la imagen).
/// </summary>
public record ResultadoCobertura(
    double BosquePorcentajeCorte,
    double BosquePorcentajeActual,
    DateOnly FechaImagenActual,
    string Fuente,
    string? Aviso = null);

/// <summary>Momento de una imagen satelital del predio (RF-08): la referencia de 2020 o la más reciente.</summary>
public enum MomentoImagen { Referencia, Actual }

/// <summary>
/// RF-08. Imagen en color natural del entorno del predio. Limites = oeste, sur, este y norte en WGS84, para dibujar
/// el contorno del predio encima. Periodo = las fechas de las imágenes que se juntaron.
/// </summary>
public record ImagenSatelital(byte[] Contenido, string Tipo, string Periodo, double[] Limites, string Fuente);

/// <summary>Interfaz propia de LINDE para el análisis de cobertura. Ninguna validación depende de Earth Engine.</summary>
public interface IProveedorCobertura
{
    Task<ResultadoCobertura> AnalizarAsync(IReadOnlyList<Coordenada> vertices, DateOnly fechaCorte, CancellationToken ct = default);

    /// <summary>RF-08. Imagen del predio en ese momento; null si el proveedor no tiene imágenes (el simulado).</summary>
    Task<ImagenSatelital?> ImagenAsync(IReadOnlyList<Coordenada> vertices, MomentoImagen momento, CancellationToken ct = default);
}
