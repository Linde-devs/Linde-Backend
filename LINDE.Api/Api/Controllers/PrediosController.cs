using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Dominio;
using LINDE.Api.Servicios;
using LINDE.Api.Validacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>CU-03 (gestionar predios), CU-06 (enviar a revisión) y CU-07 (validar que tenga todo lo necesario).
/// El productor solo ve y modifica sus predios (lo controla el servicio, RNF-03).</summary>
[ApiController]
[Route("api/predios")]
public class PrediosController(IServicioPredios predios, IServicioValidacionPredios validacion, VistasPredio vistas) : ControllerBase
{
    [HttpGet]
    public IEnumerable<PredioVista> Listar([FromQuery] int? productorId) => predios.Listar(productorId).Select(vistas.Crear);

    [HttpGet("{id:int}")]
    public PredioVista Obtener(int id) => vistas.Crear(predios.Obtener(id));

    [HttpPost]
    [Authorize(Roles = Roles.ProductorOAdministrador)]
    public ActionResult<PredioVista> Crear(PredioDto dto)
    {
        var predio = predios.Crear(dto);
        return CreatedAtAction(nameof(Obtener), new { id = predio.Id }, vistas.Crear(predio));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.ProductorOAdministrador)]
    public PredioVista Actualizar(int id, PredioDto dto) => vistas.Crear(predios.Actualizar(id, dto));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.ProductorOAdministrador)]
    public IActionResult Eliminar(int id)
    {
        predios.Eliminar(id);
        return NoContent();
    }

    /// <summary>Facade: una sola llamada ejecuta las cuatro capas, genera alertas y cambia el estado.</summary>
    [HttpPost("{id:int}/enviar")]
    [Authorize(Roles = Roles.ProductorOAdministrador)]
    public Task<ResultadoValidacion> Enviar(int id, CancellationToken ct) => validacion.EnviarARevisionAsync(id, ct);

    /// <summary>CU-07. Validar predio: lista de requisitos para enviarlo, cumplidos o pendientes. No guarda nada.</summary>
    [HttpGet("{id:int}/requisitos")]
    public RevisionRequisitos Requisitos(int id) => validacion.RevisarRequisitos(id);

    [HttpGet("{id:int}/validaciones")]
    public IReadOnlyList<ResultadoValidacion> Validaciones(int id) => validacion.Historial(id);
}
