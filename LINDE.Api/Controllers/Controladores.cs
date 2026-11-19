using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Configuracion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Expedientes;
using LINDE.Api.Geometria;
using LINDE.Api.Servicios;
using LINDE.Api.Validacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LINDE.Api.Controllers;

/// <summary>CU-02. Gestionar productores.</summary>
[ApiController]
[Route("api/productores")]
public class ProductoresController(ServicioProductores servicio) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<Productor> Listar() => servicio.Listar();

    [HttpGet("{id:int}")]
    public Productor Obtener(int id) => servicio.Obtener(id);

    [HttpPost]
    public ActionResult<Productor> Crear(ProductorDto dto)
    {
        var productor = servicio.Crear(dto);
        return CreatedAtAction(nameof(Obtener), new { id = productor.Id }, productor);
    }

    [HttpPut("{id:int}")]
    public Productor Actualizar(int id, ProductorDto dto) => servicio.Actualizar(id, dto);

    [HttpDelete("{id:int}")]
    public IActionResult Eliminar(int id)
    {
        servicio.Eliminar(id);
        return NoContent();
    }
}

/// <summary>CU-03 (gestionar predios), CU-06 (enviar a revisión) y CU-09 (resolver casos en revisión).</summary>
[ApiController]
[Route("api/predios")]
public class PrediosController(IServicioPredios predios, IServicioValidacionPredios validacion) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<Predio> Listar([FromQuery] int? productorId) => predios.Listar(productorId);

    [HttpGet("{id:int}")]
    public Predio Obtener(int id) => predios.Obtener(id);

    [HttpPost]
    public ActionResult<Predio> Crear(PredioDto dto)
    {
        var predio = predios.Crear(dto);
        return CreatedAtAction(nameof(Obtener), new { id = predio.Id }, predio);
    }

    [HttpPut("{id:int}")]
    public Predio Actualizar(int id, PredioDto dto) => predios.Actualizar(id, dto);

    [HttpDelete("{id:int}")]
    public IActionResult Eliminar(int id)
    {
        predios.Eliminar(id);
        return NoContent();
    }

    /// <summary>Facade: una sola llamada ejecuta las cuatro capas, genera alertas y cambia el estado.</summary>
    [HttpPost("{id:int}/enviar")]
    public Task<ResultadoValidacion> Enviar(int id, CancellationToken ct) => validacion.EnviarARevisionAsync(id, ct);

    [HttpGet("{id:int}/validaciones")]
    public IReadOnlyList<ResultadoValidacion> Validaciones(int id) => validacion.Historial(id);

    [HttpPost("{id:int}/aprobar")]
    [Authorize(Roles = Roles.VerificadorOAdministrador)]
    public Predio Aprobar(int id, ResolucionDto dto) => predios.Aprobar(id, dto.Comentario);

    [HttpPost("{id:int}/observar")]
    [Authorize(Roles = Roles.VerificadorOAdministrador)]
    public Predio Observar(int id, ResolucionDto dto) => predios.Observar(id, dto.Comentario ?? "");
}

/// <summary>CU-04. Gestionar polígonos.</summary>
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
    public PoligonoVista Registrar(int predioId, VerticesDto dto) =>
        Vista(servicio.Registrar(predioId, dto.Vertices.Select(v => new Coordenada(v.Lon, v.Lat)).ToList(), dto.Motivo));

    [HttpPut("vertices/{indice:int}")]
    public PoligonoVista EditarVertice(int predioId, int indice, EditarVerticeDto dto) =>
        Vista(servicio.EditarVertice(predioId, indice, new Coordenada(dto.Lon, dto.Lat), dto.Motivo));

    /// <summary>Acepta .geojson, .json y .kml. La fábrica elige el lector según la extensión.</summary>
    [HttpPost("importar")]
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

        return new PoligonoVista(p.Id, p.PredioId, p.Version, p.Vigente, p.EsPunto, area, p.Fecha, p.Autor, p.Motivo, p.Vertices);
    }
}

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
}

/// <summary>CU-08. Gestionar alertas (bandeja del verificador, ordenada por prioridad).</summary>
[ApiController]
[Route("api/alertas")]
public class AlertasController(IRepositorio<Alerta> alertas) : ControllerBase
{
    [HttpGet]
    public IEnumerable<Alerta> Listar([FromQuery] bool soloPendientes = true) =>
        alertas.Listar(a => !soloPendientes || !a.Resuelta)
            .OrderBy(a => a.Prioridad)
            .ThenBy(a => a.Fecha);
}

/// <summary>CU-10. Gestionar expedientes.</summary>
[ApiController]
[Route("api/expedientes")]
public class ExpedientesController(ServicioExpedientes servicio) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<Expediente> Listar() => servicio.Listar();

    [HttpPost("predio/{predioId:int}")]
    public Expediente Generar(int predioId) => servicio.Generar(predioId);

    [HttpGet("{codigo}")]
    public Expediente Obtener(string codigo) => servicio.Obtener(codigo);

    [HttpGet("{codigo}/geojson")]
    public ContentResult GeoJson(string codigo) =>
        Content(FormatosExpediente.AGeoJson(servicio.Obtener(codigo)), "application/geo+json");
}

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

/// <summary>CU-01. Registro, inicio de sesión y datos de la sesión actual.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(ServicioAutenticacion servicio) : ControllerBase
{
    [HttpPost("registro")]
    [AllowAnonymous]
    public ActionResult<UsuarioDto> Registrar(RegistroDto dto)
    {
        var usuario = servicio.Registrar(dto);
        return CreatedAtAction(nameof(Yo), null, usuario);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public RespuestaLogin IniciarSesion(LoginDto dto) => servicio.IniciarSesion(dto);

    /// <summary>El frontend lo usa al recargar la página para recuperar la sesión a partir del token.</summary>
    [HttpGet("yo")]
    public UsuarioDto Yo() => servicio.Obtener(int.Parse(User.FindFirst(Claims.Id)!.Value));
}
