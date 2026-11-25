using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using Microsoft.Extensions.Options;

namespace LINDE.Api.Cobertura;

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

    /// <summary>El proveedor simulado no tiene imágenes: la pantalla avisa que hace falta Earth Engine.</summary>
    public Task<ImagenSatelital?> ImagenAsync(IReadOnlyList<Coordenada> vertices, MomentoImagen momento, CancellationToken ct = default) =>
        Task.FromResult<ImagenSatelital?>(null);
}
