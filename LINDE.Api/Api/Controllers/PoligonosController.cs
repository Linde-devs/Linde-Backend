using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using LINDE.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>CU-04. Gestionar polígonos. Una geometría inválida se rechaza al cargarla (RF-03).</summary>
[ApiController]
[Route("api/predios/{predioId:int}/poligono")]
public class PoligonosController(IServicioPoligonos servicio) : ControllerBase
{
    [HttpGet]
    public ActionResult<PoligonoVista> Vigente(int predioId) =>
        servicio.ObtenerVigente(predioId) is { } p ? Vista(p) : NotFound(new { error = "El predio todavía no tiene polígono." });

    [HttpGet("historial")]
    public IEnumerable<PoligonoVista> Historial(int predioId) => servicio.Historial(predioId).Select(Vista);

    [HttpPut]
    [Authorize(Roles = Roles.ProductorOAdministrador)]
    public PoligonoVista Registrar(int predioId, VerticesDto dto) =>
        Vista(servicio.Registrar(predioId, dto.Vertices.Select(v => new Coordenada(v.Lon, v.Lat)).ToList(), dto.Motivo,
            dto.Vertices.Select(v => v.PrecisionM).ToList()));

    [HttpPut("vertices/{indice:int}")]
    [Authorize(Roles = Roles.ProductorOAdministrador)]
    public PoligonoVista EditarVertice(int predioId, int indice, EditarVerticeDto dto) =>
        Vista(servicio.EditarVertice(predioId, indice, new Coordenada(dto.Lon, dto.Lat), dto.Motivo));

    /// <summary>Acepta .geojson, .json y .kml. La fábrica elige el lector según la extensión.</summary>
    [HttpPost("importar")]
    [Authorize(Roles = Roles.ProductorOAdministrador)]
    [Consumes("multipart/form-data")]
    public PoligonoVista Importar(int predioId, IFormFile archivo, [FromForm] string? motivo)
    {
        using var contenido = archivo.OpenReadStream();
        return Vista(servicio.Importar(predioId, archivo.FileName, contenido, motivo));
    }

    private static PoligonoVista Vista(Poligono p)
    {
        double? area = null;
        if (!p.EsPunto && p.Vertices.Count >= 4 && p.Vertices[0] == p.Vertices[^1])
        {
            try { area = Math.Round(Geometrias.AreaHectareas(p.Vertices), 4); }
            catch (ArgumentException) { area = null; }
        }

        return new PoligonoVista(p.Id, p.PredioId, p.Version, p.Vigente, p.EsPunto, area, p.Fecha, p.Autor, p.Motivo, p.Vertices,
            p.PrecisionesM);
    }
}
