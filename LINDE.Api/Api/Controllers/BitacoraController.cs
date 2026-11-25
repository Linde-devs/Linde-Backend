using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Dominio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>Consulta de la bitácora de auditoría.</summary>
[ApiController]
[Route("api/bitacora")]
[Authorize(Roles = Roles.Administrador)]
public class BitacoraController(IBitacora bitacora) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<RegistroBitacora> Listar([FromQuery] string? entidad, [FromQuery] string? entidadId) =>
        bitacora.Listar(entidad, entidadId);
}
