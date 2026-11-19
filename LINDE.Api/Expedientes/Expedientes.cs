using System.Text.Json;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;

namespace LINDE.Api.Expedientes;

/// <summary>Expediente digital del predio aprobado (CU-10). No es la certificación: la emite el SENASAG.</summary>
public class Expediente : IEntidad
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public DateTime FechaGeneracion { get; set; } = DateTime.UtcNow;
    public string Productor { get; set; } = "";
    public string CiProductor { get; set; } = "";
    public int PredioId { get; set; }
    public string Predio { get; set; } = "";
    public string Municipio { get; set; } = "";
    public double SuperficieDeclaradaHa { get; set; }
    public double AreaPoligonoHa { get; set; }
    public int VersionPoligono { get; set; }
    public List<Coordenada> Vertices { get; set; } = new();
    public List<ResultadoCapa> Validaciones { get; set; } = new();
    public List<string> Documentos { get; set; } = new();
    public string? ResolucionVerificador { get; set; }
}

/// <summary>
/// Patrón Builder: recibe cada sección por separado y solo al final, en Construir, verifica que el expediente
/// esté completo y que el predio esté aprobado. Así nunca se genera el expediente de un predio no aprobado.
/// </summary>
public class ExpedienteBuilder
{
    private Productor? _productor;
    private Predio? _predio;
    private Poligono? _poligono;
    private ResultadoValidacion? _validacion;
    private readonly List<Documento> _documentos = new();
    private string? _resolucion;

    public ExpedienteBuilder ConProductor(Productor productor) { _productor = productor; return this; }

    public ExpedienteBuilder ConPredio(Predio predio) { _predio = predio; return this; }

    public ExpedienteBuilder ConPoligono(Poligono poligono) { _poligono = poligono; return this; }

    public ExpedienteBuilder ConValidacion(ResultadoValidacion validacion) { _validacion = validacion; return this; }

    public ExpedienteBuilder ConDocumentos(IEnumerable<Documento> documentos) { _documentos.AddRange(documentos); return this; }

    /// <summary>Sección opcional: solo existe si el predio tuvo alertas y lo resolvió un verificador.</summary>
    public ExpedienteBuilder ConResolucionVerificador(string? comentario) { _resolucion = comentario; return this; }

    public Expediente Construir(string codigo)
    {
        var faltantes = new List<string>();
        if (_productor is null) faltantes.Add("productor");
        if (_predio is null) faltantes.Add("predio");
        if (_poligono is null) faltantes.Add("polígono");
        if (_validacion is null) faltantes.Add("resultado de validación");
        if (faltantes.Count > 0)
            throw new OperacionNoPermitidaException($"El expediente está incompleto. Falta: {string.Join(", ", faltantes)}.");

        _predio!.VerificarGeneracionExpediente();

        if (_validacion!.Cortado)
            throw new OperacionNoPermitidaException("La última validación fue rechazada por la capa geométrica.");

        return new Expediente
        {
            Codigo = codigo,
            Productor = $"{_productor!.Nombre} {_productor.Apellido}",
            CiProductor = _productor.Ci,
            PredioId = _predio.Id,
            Predio = _predio.Nombre,
            Municipio = _predio.Municipio,
            SuperficieDeclaradaHa = _predio.SuperficieDeclaradaHa,
            AreaPoligonoHa = _poligono!.EsPunto ? _predio.SuperficieDeclaradaHa : Math.Round(Geometrias.AreaHectareas(_poligono.Vertices), 4),
            VersionPoligono = _poligono.Version,
            Vertices = new List<Coordenada>(_poligono.Vertices),
            Validaciones = _validacion.Capas.ToList(),
            Documentos = _documentos.Select(d => $"{d.Tipo} {d.Numero}".Trim()).ToList(),
            ResolucionVerificador = _resolucion
        };
    }
}

/// <summary>Representaciones del mismo expediente. El PDF se agregará con una librería de reportes.</summary>
public static class FormatosExpediente
{
    /// <summary>GeoJSON estándar (RNF-06) para el comprador: una Feature con el polígono y los datos del expediente.</summary>
    public static string AGeoJson(Expediente e)
    {
        object geometria = e.Vertices.Count == 1
            ? new { type = "Point", coordinates = new[] { e.Vertices[0].Lon, e.Vertices[0].Lat } }
            : new { type = "Polygon", coordinates = new[] { e.Vertices.Select(v => new[] { v.Lon, v.Lat }).ToArray() } };

        var feature = new
        {
            type = "Feature",
            geometry = geometria,
            properties = new
            {
                codigo = e.Codigo,
                predio = e.Predio,
                municipio = e.Municipio,
                productor = e.Productor,
                areaHa = e.AreaPoligonoHa,
                versionPoligono = e.VersionPoligono,
                fechaGeneracion = e.FechaGeneracion,
                validaciones = e.Validaciones.Select(v => new { capa = v.Capa.ToString(), estado = v.Estado.ToString(), v.Mensaje })
            }
        };

        return JsonSerializer.Serialize(feature, new JsonSerializerOptions { WriteIndented = true });
    }
}

public class ServicioExpedientes(
    IRepositorio<Predio> predios,
    IRepositorio<Productor> productores,
    IRepositorio<Poligono> poligonos,
    IRepositorio<Documento> documentos,
    IRepositorio<ResultadoValidacion> resultados,
    IRepositorio<Expediente> expedientes)
{
    public Expediente Generar(int predioId)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        var builder = new ExpedienteBuilder().ConPredio(predio);

        if (productores.Obtener(predio.ProductorId) is { } productor) builder.ConProductor(productor);
        if (poligonos.Listar(p => p.PredioId == predioId && p.Vigente).FirstOrDefault() is { } poligono) builder.ConPoligono(poligono);
        if (resultados.Listar(r => r.PredioId == predioId).OrderByDescending(r => r.Fecha).FirstOrDefault() is { } ultima)
            builder.ConValidacion(ultima);

        builder.ConDocumentos(documentos.Listar(d => d.PredioId == predioId))
               .ConResolucionVerificador(predio.ComentarioVerificador);

        var codigo = $"LINDE-{DateTime.UtcNow:yyyy}-{predioId:D6}";
        var expediente = builder.Construir(codigo);

        var anterior = expedientes.Listar(e => e.Codigo == codigo).FirstOrDefault();
        if (anterior is not null) expedientes.Eliminar(anterior.Id);
        return expedientes.Agregar(expediente);
    }

    public Expediente Obtener(string codigo) =>
        expedientes.Listar(e => e.Codigo == codigo).FirstOrDefault()
        ?? throw new RecursoNoEncontradoException($"No existe el expediente {codigo}.");

    public IReadOnlyList<Expediente> Listar() => expedientes.Listar();
}
