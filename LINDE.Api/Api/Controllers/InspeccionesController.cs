using LINDE.Api.Contratos;
using LINDE.Api.Dominio;
using LINDE.Api.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>RF-14 (CU-09). Inspecciones de campo que registra el productor con el resultado del técnico.</summary>
[ApiController]
[Route("api")]
public class InspeccionesController(IServicioInspecciones servicio) : ControllerBase
{
    [HttpGet("predios/{predioId:int}/inspecciones")]
    public IReadOnlyList<InspeccionCampo> Listar(int predioId) => servicio.Listar(predioId);

    [HttpPost("predios/{predioId:int}/inspecciones")]
    public InspeccionCampo Registrar(int predioId, InspeccionDto dto) => servicio.Registrar(predioId, dto);

    [HttpDelete("inspecciones/{id:int}")]
    public IActionResult Eliminar(int id)
    {
        servicio.Eliminar(id);
        return NoContent();
    }
}
