using System.Globalization;
using System.Text;
using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Dominio;
using LINDE.Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace LINDE.Api.Controllers;

/// <summary>CU-11. Gestionar capas de referencia (títulos del INRA y autorizaciones de la ABT). Solo el administrador.</summary>
[ApiController]
[Route("api/capas")]
[Authorize(Roles = Roles.Administrador)]
public class CapasController(IServicioCapas servicio) : ControllerBase
{
    [HttpGet]
    public IEnumerable<CapaVista> Listar() => servicio.Listar().Select(Vista);

    /// <summary>Carga un GeoJSON (FeatureCollection) como la capa vigente de su tipo. Límite: 20 MB.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public ActionResult<CapaVista> Cargar(IFormFile archivo, [FromForm] TipoCapa tipo, [FromForm] string nombre, [FromForm] string fuente)
    {
        using var contenido = archivo.OpenReadStream();
        var capa = servicio.Cargar(tipo, nombre, fuente, archivo.FileName, contenido);
        return Created($"/api/capas/{capa.Id}", Vista(capa));
    }

    /// <summary>
    /// La capa en GeoJSON, para verla en el mapa. No incluye el CI de los titulares: es un dato personal
    /// que solo se usa para el cruce.
    /// </summary>
    [HttpGet("{id:int}/geojson")]
    public ContentResult GeoJson(int id)
    {
        var sb = new StringBuilder("{\"type\":\"FeatureCollection\",\"features\":[");
        var primero = true;
        foreach (var e in servicio.Elementos(id))
        {
            if (!primero) sb.Append(',');
            primero = false;
            sb.Append("{\"type\":\"Feature\",\"properties\":{\"codigo\":")
              .Append(System.Text.Json.JsonSerializer.Serialize(e.Codigo))
              .Append(",\"superficie_ha\":")
              .Append(e.SuperficieHa?.ToString(CultureInfo.InvariantCulture) ?? "null")
              .Append("},\"geometry\":");
            EscribirGeometria(sb, e.Geometria);
            sb.Append('}');
        }
        sb.Append("]}");
        return Content(sb.ToString(), "application/geo+json");
    }

    [HttpDelete("{id:int}")]
    public IActionResult Eliminar(int id)
    {
        servicio.Eliminar(id);
        return NoContent();
    }

    private static CapaVista Vista(CapaReferencia c) =>
        new(c.Id, c.Tipo, c.Nombre, c.Fuente, c.Archivo, c.CantidadElementos, c.FechaCarga, c.CargadaPor, c.Vigente);

    private static void EscribirGeometria(StringBuilder sb, Geometry g)
    {
        static void Anillo(StringBuilder sb, LineString anillo)
        {
            sb.Append('[');
            for (var i = 0; i < anillo.NumPoints; i++)
            {
                if (i > 0) sb.Append(',');
                var c = anillo.GetCoordinateN(i);
                sb.Append('[').Append(c.X.ToString("0.#######", CultureInfo.InvariantCulture)).Append(',')
                  .Append(c.Y.ToString("0.#######", CultureInfo.InvariantCulture)).Append(']');
            }
            sb.Append(']');
        }

        static void Poligono(StringBuilder sb, Polygon p)
        {
            sb.Append('[');
            Anillo(sb, p.ExteriorRing);
            foreach (var hueco in p.InteriorRings) { sb.Append(','); Anillo(sb, hueco); }
            sb.Append(']');
        }

        switch (g)
        {
            case Polygon p:
                sb.Append("{\"type\":\"Polygon\",\"coordinates\":");
                Poligono(sb, p);
                sb.Append('}');
                break;
            case MultiPolygon mp:
                sb.Append("{\"type\":\"MultiPolygon\",\"coordinates\":[");
                for (var i = 0; i < mp.NumGeometries; i++)
                {
                    if (i > 0) sb.Append(',');
                    Poligono(sb, (Polygon)mp.GetGeometryN(i));
                }
                sb.Append("]}");
                break;
            default:
                sb.Append("null");
                break;
        }
    }
}
