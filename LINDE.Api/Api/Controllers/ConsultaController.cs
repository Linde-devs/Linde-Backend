using LINDE.Api.Expedientes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>
/// RF-15. Consulta pública del expediente por su código. No necesita cuenta: la usan el técnico o el SENASAG
/// para comprobar el expediente que les muestra el productor.
/// </summary>
[ApiController]
[Route("api/consulta")]
[AllowAnonymous]
public class ConsultaController(ServicioExpedientes servicio) : ControllerBase
{
    [HttpGet("{codigo}")]
    public Expediente Obtener(string codigo) => servicio.ConsultarPublico(codigo);
}
