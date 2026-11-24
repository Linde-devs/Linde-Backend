using LINDE.Api.Auditoria;
using LINDE.Api.Cobertura;
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
using Microsoft.Extensions.Options;

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
        Vista(servicio.Registrar(predioId, dto.Vertices.Select(v => new Coordenada(v.Lon, v.Lat)).ToList(), dto.Motivo));

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

/// <summary>RF-14 (CU-09). Inspecciones de campo que registra el productor con el resultado del técnico.</summary>
[ApiController]
[Route("api")]
public class InspeccionesController(IServicioInspecciones servicio) : ControllerBase
{
    [HttpGet("predios/{predioId:int}/inspecciones")]
    public IReadOnlyList<InspeccionCampo> Listar(int predioId) => servicio.Listar(predioId);

    [HttpPost("predios/{predioId:int}/inspecciones")]
    public InspeccionCampo Registrar(int predioId, InspeccionDto dto) => servicio.Registrar(predioId, dto);

    [HttpDelete("inspecciones/{id:int}")]
    public IActionResult Eliminar(int id)
    {
        servicio.Eliminar(id);
        return NoContent();
    }
}

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

/// <summary>CU-10. Gestionar expedientes: el productor genera el de su predio, aprobado o con alertas.</summary>
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

/// <summary>
/// RF-15. Consulta pública del expediente por su código. No necesita cuenta: la usan el técnico o el SENASAG
/// para comprobar el expediente que les muestra el productor.
/// </summary>
[ApiController]
[Route("api/consulta")]
[AllowAnonymous]
public class ConsultaController(ServicioExpedientes servicio) : ControllerBase
{
    [HttpGet("{codigo}")]
    public Expediente Obtener(string codigo) => servicio.ConsultarPublico(codigo);
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

/// <summary>
/// Prueba de la conexión con Earth Engine. El administrador la usa después de configurar la llave para comprobar que
/// todo funciona sin tener que enviar un predio. Llama directo al adaptador, sin pasar por la caché del proxy.
/// </summary>
[ApiController]
[Route("api/cobertura")]
[Authorize(Roles = Roles.Administrador)]
public class CoberturaController(IOptions<OpcionesCobertura> opciones, IServiceProvider servicios) : ControllerBase
{
    // Unas 100 ha cerca de San Julián (Santa Cruz).
    private static readonly List<Coordenada> PoligonoDePrueba =
    [
        new(-62.6300, -16.9100), new(-62.6205, -16.9100), new(-62.6205, -16.9195), new(-62.6300, -16.9195), new(-62.6300, -16.9100)
    ];

    [HttpGet("prueba")]
    public async Task<IActionResult> Probar(CancellationToken ct)
    {
        if (!opciones.Value.Proveedor.Equals("EarthEngine", StringComparison.OrdinalIgnoreCase))
            throw new DatosInvalidosException($"El proveedor de cobertura configurado es \"{opciones.Value.Proveedor}\". Cambia Cobertura:Proveedor a \"EarthEngine\".");

        try
        {
            var r = await servicios.GetRequiredService<EarthEngineAdapter>().AnalizarAsync(PoligonoDePrueba, new DateOnly(2020, 12, 31), ct);
            return Ok(new { conectado = true, r.BosquePorcentajeCorte, r.BosquePorcentajeActual, r.FechaImagenActual, r.Fuente });
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return Ok(new { conectado = false, error = e.Message });
        }
    }
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
