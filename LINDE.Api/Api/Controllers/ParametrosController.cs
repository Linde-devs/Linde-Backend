using LINDE.Api.Autenticacion;
using LINDE.Api.Configuracion;
using LINDE.Api.Dominio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>CU-12. Gestionar parámetros.</summary>
[ApiController]
[Route("api/parametros")]
public class ParametrosController(IAdministracionParametros administracion) : ControllerBase
{
    [HttpGet]
    public Parametros Obtener() => administracion.Obtener();

    [HttpPut]
    [Authorize(Roles = Roles.Administrador)]
    public Parametros Actualizar(Parametros parametros) => administracion.Actualizar(parametros);
}
