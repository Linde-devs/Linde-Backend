using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>CU-02. Gestionar productores. Solo el administrador.</summary>
[ApiController]
[Route("api/productores")]
[Authorize(Roles = Roles.Administrador)]
public class ProductoresController(ServicioProductores servicio) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<ProductorVista> Listar() => servicio.Listar();

    [HttpGet("{id:int}")]
    public ProductorVista Obtener(int id) => servicio.Obtener(id);

    /// <summary>El administrador registra un productor con su cuenta de acceso.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public ActionResult<ProductorVista> Crear(RegistroProductorDto dto)
    {
        var productor = servicio.Registrar(dto);
        return CreatedAtAction(nameof(Obtener), new { id = productor.Id }, productor);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public ProductorVista Actualizar(int id, ProductorDto dto) => servicio.Actualizar(id, dto);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public IActionResult Eliminar(int id)
    {
        servicio.Eliminar(id);
        return NoContent();
    }
}
