using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>
/// CU-08. Alertas que generaron las validaciones, ordenadas por prioridad (RF-13).
/// El productor ve las de sus predios; el administrador, todas.
/// </summary>
[ApiController]
[Route("api/alertas")]
public class AlertasController(IRepositorio<Alerta> alertas, IRepositorio<Predio> predios, ControlAcceso acceso) : ControllerBase
{
    [HttpGet]
    public IEnumerable<AlertaVista> Listar([FromQuery] int? predioId, [FromQuery] bool soloPendientes = true)
    {
        var visibles = predios.Listar(p => acceso.ProductorPropio == null || p.ProductorId == acceso.ProductorPropio)
            .ToDictionary(p => p.Id, p => p.Nombre);
        var ids = visibles.Keys.ToList();

        return alertas.Listar(a => ids.Contains(a.PredioId) && (predioId == null || a.PredioId == predioId) && (!soloPendientes || !a.Resuelta))
            .OrderBy(a => a.Prioridad)
            .ThenByDescending(a => a.Fecha)
            .Select(a => new AlertaVista(a.Id, a.PredioId, visibles[a.PredioId], a.Capa, a.Prioridad, a.Mensaje, a.Fecha, a.Resuelta));
    }
}
