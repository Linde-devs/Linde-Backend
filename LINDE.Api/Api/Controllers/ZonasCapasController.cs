using LINDE.Api.Validacion;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>RF-07. Zonas del predio fuera del título del INRA o de las autorizaciones de la ABT, para el mapa.</summary>
[ApiController]
[Route("api/predios/{predioId:int}/zonas-capas")]
public class ZonasCapasController(ServicioZonasCapas servicio) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<ZonaVista> Obtener(int predioId) => servicio.Calcular(predioId);
}
