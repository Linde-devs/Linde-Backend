using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>CU-01. Registro, inicio de sesión y datos de la sesión actual.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(ServicioAutenticacion servicio, ServicioProductores productores) : ControllerBase
{
    /// <summary>RF-01. Registro público: crea el productor y su cuenta con rol Productor.</summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    public ActionResult<ProductorVista> Registrar(RegistroProductorDto dto)
    {
        var productor = productores.Registrar(dto);
        return CreatedAtAction(nameof(Yo), null, productor);
    }

    /// <summary>Registro del administrador (Sprint 1). Solo otro administrador puede crearlo.</summary>
    [HttpPost("administradores")]
    [Authorize(Roles = Roles.Administrador)]
    public ActionResult<UsuarioDto> RegistrarAdministrador(RegistroAdministradorDto dto)
    {
        var usuario = servicio.RegistrarAdministrador(dto);
        return CreatedAtAction(nameof(Yo), null, usuario);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public RespuestaLogin IniciarSesion(LoginDto dto) => servicio.IniciarSesion(dto);

    /// <summary>El frontend lo usa al recargar la página para recuperar la sesión a partir del token.</summary>
    [HttpGet("yo")]
    public UsuarioDto Yo() => servicio.Obtener(int.Parse(User.FindFirst(Claims.Id)!.Value));
}
