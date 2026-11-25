using LINDE.Api.Cobertura;
using LINDE.Api.Autenticacion;
using LINDE.Api.Dominio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LINDE.Api.Controllers;

/// <summary>
/// Prueba de la conexión con Earth Engine. El administrador la usa después de configurar la llave para comprobar que
/// todo funciona sin tener que enviar un predio. Llama directo al adaptador, sin pasar por la caché del proxy.
/// </summary>
[ApiController]
[Route("api/cobertura")]
[Authorize(Roles = Roles.Administrador)]
public class CoberturaController(IOptions<OpcionesCobertura> opciones, IServiceProvider servicios) : ControllerBase
{
    // Unas 100 ha cerca de San Julián (Santa Cruz).
    private static readonly List<Coordenada> PoligonoDePrueba =
    [
        new(-62.6300, -16.9100), new(-62.6205, -16.9100), new(-62.6205, -16.9195), new(-62.6300, -16.9195), new(-62.6300, -16.9100)
    ];

    [HttpGet("prueba")]
    public async Task<IActionResult> Probar(CancellationToken ct)
    {
        if (!opciones.Value.Proveedor.Equals("EarthEngine", StringComparison.OrdinalIgnoreCase))
            throw new DatosInvalidosException($"El proveedor de cobertura configurado es \"{opciones.Value.Proveedor}\". Cambia Cobertura:Proveedor a \"EarthEngine\".");

        try
        {
            var r = await servicios.GetRequiredService<EarthEngineAdapter>().AnalizarAsync(PoligonoDePrueba, new DateOnly(2020, 12, 31), ct);
            return Ok(new { conectado = true, r.BosquePorcentajeCorte, r.BosquePorcentajeActual, r.FechaImagenActual, r.Fuente, r.Aviso });
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return Ok(new { conectado = false, error = e.Message });
        }
    }
}
