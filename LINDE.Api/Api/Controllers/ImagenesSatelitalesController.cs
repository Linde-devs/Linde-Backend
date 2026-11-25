using LINDE.Api.Cobertura;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>RF-08. Imágenes satelitales del predio en 2020 y ahora, para compararlas a simple vista.</summary>
[ApiController]
[Route("api/predios/{predioId:int}/imagenes-satelitales")]
public class ImagenesSatelitalesController(ServicioImagenesPredio servicio) : ControllerBase
{
    [HttpGet]
    public Task<ImagenesPredio> Obtener(int predioId, CancellationToken ct) => servicio.ObtenerAsync(predioId, ct);
}
