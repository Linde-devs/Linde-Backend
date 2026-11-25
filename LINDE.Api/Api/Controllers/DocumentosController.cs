using LINDE.Api.Contratos;
using LINDE.Api.Dominio;
using LINDE.Api.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>CU-05. Gestionar documentos.</summary>
[ApiController]
[Route("api")]
public class DocumentosController(IServicioDocumentos servicio) : ControllerBase
{
    [HttpGet("predios/{predioId:int}/documentos")]
    public IReadOnlyList<Documento> Listar(int predioId) => servicio.Listar(predioId);

    [HttpPost("predios/{predioId:int}/documentos")]
    public Documento Agregar(int predioId, DocumentoDto dto) => servicio.Agregar(predioId, dto);

    [HttpDelete("documentos/{id:int}")]
    public IActionResult Eliminar(int id)
    {
        servicio.Eliminar(id);
        return NoContent();
    }

    /// <summary>RF-16. Sube el PDF o la imagen del documento (máximo 10 MB). Si ya tenía uno, lo reemplaza.</summary>
    [HttpPost("documentos/{id:int}/archivo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ServicioDocumentos.TamanoMaximoBytes + 1024 * 1024)]
    public Documento SubirArchivo(int id, IFormFile archivo)
    {
        using var contenido = archivo.OpenReadStream();
        return servicio.AdjuntarArchivo(id, archivo.FileName, contenido);
    }

    /// <summary>RF-16. Devuelve el archivo para verlo en el navegador o descargarlo.</summary>
    [HttpGet("documentos/{id:int}/archivo")]
    public IActionResult VerArchivo(int id)
    {
        var archivo = servicio.AbrirArchivo(id);
        return File(archivo.Contenido, archivo.Tipo, archivo.Nombre, enableRangeProcessing: false);
    }
}
