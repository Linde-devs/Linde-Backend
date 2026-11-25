using LINDE.Api.Expedientes;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>CU-10. Gestionar expedientes: el productor genera el de su predio, aprobado o con alertas.</summary>
[ApiController]
[Route("api/expedientes")]
public class ExpedientesController(ServicioExpedientes servicio) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<Expediente> Listar() => servicio.Listar();

    [HttpPost("predio/{predioId:int}")]
    public Expediente Generar(int predioId) => servicio.Generar(predioId);

    [HttpGet("{codigo}")]
    public Expediente Obtener(string codigo) => servicio.Obtener(codigo);

    [HttpGet("{codigo}/geojson")]
    public ContentResult GeoJson(string codigo) =>
        Content(FormatosExpediente.AGeoJson(servicio.Obtener(codigo)), "application/geo+json");
}
