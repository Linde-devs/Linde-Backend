using System.Text.Json;
using LINDE.Api.Autenticacion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;

namespace LINDE.Api.Expedientes;

/// <summary>Estado del expediente: conforme (las cuatro capas sin observaciones) o con alertas.</summary>
public enum EstadoInforme
{
    Conforme,
    ConAlertas
}

/// <summary>Una alerta tal como queda escrita en el expediente.</summary>
public record AlertaExpediente(CapaValidacion Capa, PrioridadAlerta Prioridad, string Mensaje);

/// <summary>RF-14. Inspección de campo tal como queda en el expediente: fecha, técnico, resultado y comentario.</summary>
public record InspeccionExpediente(DateOnly Fecha, string Tecnico, ResultadoInspeccion Resultado, string? Comentario);

/// <summary>
/// Expediente digital del predio (CU-10, RF-09). Se genera para predios aprobados y también para predios con alertas,
/// indicando su estado. El productor lo muestra al técnico o al SENASAG, que pueden consultarlo por su código
/// sin tener cuenta. No es la certificación: la emite el SENASAG fuera del sistema.
/// </summary>
public class Expediente : IEntidad
{
    public const string Leyenda =
        "LINDE no emite certificaciones. Este expediente reúne la evidencia declarada y el resultado de las validaciones " +
        "automáticas. El análisis satelital es informativo y tiene un margen de error de ±5 % a ±20 %.";

    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public DateTime FechaGeneracion { get; set; } = DateTime.UtcNow;
    public string Productor { get; set; } = "";
    public string CiProductor { get; set; } = "";
    public int PredioId { get; set; }
    public string Predio { get; set; } = "";
    public string Departamento { get; set; } = "";
    public string Municipio { get; set; } = "";
    public double SuperficieDeclaradaHa { get; set; }
    public double AreaPoligonoHa { get; set; }
    public int VersionPoligono { get; set; }
    public List<Coordenada> Vertices { get; set; } = new();
    public List<ResultadoCapa> Validaciones { get; set; } = new();
    public List<string> Documentos { get; set; } = new();
    public EstadoInforme Estado { get; set; }
    public List<AlertaExpediente> Alertas { get; set; } = new();
    public List<InspeccionExpediente> Inspecciones { get; set; } = new();
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
    private readonly List<Alerta> _alertas = new();
    private readonly List<InspeccionCampo> _inspecciones = new();

    public ExpedienteBuilder ConProductor(Productor productor) { _productor = productor; return this; }

    public ExpedienteBuilder ConPredio(Predio predio) { _predio = predio; return this; }

    public ExpedienteBuilder ConPoligono(Poligono poligono) { _poligono = poligono; return this; }

    public ExpedienteBuilder ConValidacion(ResultadoValidacion validacion) { _validacion = validacion; return this; }

    public ExpedienteBuilder ConDocumentos(IEnumerable<Documento> documentos) { _documentos.AddRange(documentos); return this; }

    /// <summary>Sección que solo aparece si el predio quedó con alertas.</summary>
    public ExpedienteBuilder ConAlertas(IEnumerable<Alerta> alertas) { _alertas.AddRange(alertas); return this; }

    /// <summary>RF-14. Sección opcional: inspecciones de campo registradas para el predio.</summary>
    public ExpedienteBuilder ConInspecciones(IEnumerable<InspeccionCampo> inspecciones) { _inspecciones.AddRange(inspecciones); return this; }

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
            Departamento = _predio.Departamento,
            Municipio = _predio.Municipio,
            SuperficieDeclaradaHa = _predio.SuperficieDeclaradaHa,
            AreaPoligonoHa = _poligono!.EsPunto ? _predio.SuperficieDeclaradaHa : Math.Round(Geometrias.AreaHectareas(_poligono.Vertices), 4),
            VersionPoligono = _poligono.Version,
            Vertices = new List<Coordenada>(_poligono.Vertices),
            Validaciones = _validacion.Capas.ToList(),
            Documentos = _documentos.Select(d => $"{d.Tipo} {d.Numero}".Trim()).ToList(),
            Estado = _predio.Estado == EstadoExpediente.Aprobado ? EstadoInforme.Conforme : EstadoInforme.ConAlertas,
            Alertas = _alertas.OrderBy(a => a.Prioridad).Select(a => new AlertaExpediente(a.Capa, a.Prioridad, a.Mensaje)).ToList(),
            Inspecciones = _inspecciones.OrderByDescending(i => i.Fecha)
                .Select(i => new InspeccionExpediente(i.Fecha, i.Tecnico, i.Resultado, i.Comentario)).ToList()
        };
    }
}

/// <summary>Representaciones del mismo expediente. El PDF lo genera el navegador desde la vista imprimible del frontend.</summary>
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
                departamento = e.Departamento,
                municipio = e.Municipio,
                productor = e.Productor,
                areaHa = e.AreaPoligonoHa,
                versionPoligono = e.VersionPoligono,
                fechaGeneracion = e.FechaGeneracion,
                estado = e.Estado.ToString(),
                alertas = e.Alertas.Select(a => new { capa = a.Capa.ToString(), prioridad = a.Prioridad.ToString(), a.Mensaje }),
                validaciones = e.Validaciones.Select(v => new { capa = v.Capa.ToString(), estado = v.Estado.ToString(), v.Mensaje }),
                inspecciones = e.Inspecciones.Select(i => new
                {
                    fecha = i.Fecha.ToString("yyyy-MM-dd"), tecnico = i.Tecnico, resultado = i.Resultado.ToString(), comentario = i.Comentario
                })
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
    IRepositorio<Expediente> expedientes,
    IRepositorio<Alerta> alertas,
    ControlAcceso acceso,
    IRepositorio<InspeccionCampo>? inspecciones = null)
{
    public Expediente Generar(int predioId)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        acceso.VerificarPredio(predio);
        var builder = new ExpedienteBuilder().ConPredio(predio);

        if (productores.Obtener(predio.ProductorId) is { } productor) builder.ConProductor(productor);
        if (poligonos.Listar(p => p.PredioId == predioId && p.Vigente).FirstOrDefault() is { } poligono) builder.ConPoligono(poligono);
        if (resultados.Listar(r => r.PredioId == predioId).OrderByDescending(r => r.Fecha).FirstOrDefault() is { } ultima)
            builder.ConValidacion(ultima);

        builder.ConDocumentos(documentos.Listar(d => d.PredioId == predioId))
               .ConAlertas(alertas.Listar(a => a.PredioId == predioId && !a.Resuelta));
        if (inspecciones is not null) builder.ConInspecciones(inspecciones.Listar(i => i.PredioId == predioId));

        var codigo = $"LINDE-{DateTime.UtcNow:yyyy}-{predioId:D6}";
        var expediente = builder.Construir(codigo);

        var anterior = expedientes.Listar(e => e.Codigo == codigo).FirstOrDefault();
        if (anterior is not null) expedientes.Eliminar(anterior.Id);
        return expedientes.Agregar(expediente);
    }

    public Expediente Obtener(string codigo)
    {
        var expediente = expedientes.Listar(e => e.Codigo == codigo).FirstOrDefault()
                         ?? throw new RecursoNoEncontradoException($"No existe el expediente {codigo}.");
        if (!EsPropio(expediente)) throw new AccesoDenegadoException();
        return expediente;
    }

    /// <summary>
    /// Consulta pública por código (RF-15): la usa el técnico o el SENASAG, sin cuenta, para comprobar el expediente
    /// que le muestran. Devuelve una copia con el CI del productor enmascarado.
    /// </summary>
    public Expediente ConsultarPublico(string codigo)
    {
        var e = expedientes.Listar(x => x.Codigo == codigo.Trim().ToUpperInvariant()).FirstOrDefault()
                ?? throw new RecursoNoEncontradoException($"No existe un expediente con el código {codigo}.");
        return new Expediente
        {
            Id = e.Id, Codigo = e.Codigo, FechaGeneracion = e.FechaGeneracion, Productor = e.Productor,
            CiProductor = Enmascarar(e.CiProductor), PredioId = e.PredioId, Predio = e.Predio, Departamento = e.Departamento, Municipio = e.Municipio,
            SuperficieDeclaradaHa = e.SuperficieDeclaradaHa, AreaPoligonoHa = e.AreaPoligonoHa, VersionPoligono = e.VersionPoligono,
            Vertices = e.Vertices, Validaciones = e.Validaciones, Documentos = e.Documentos, Estado = e.Estado, Alertas = e.Alertas,
            Inspecciones = e.Inspecciones
        };
    }

    private static string Enmascarar(string ci) =>
        ci.Length <= 4 ? new string('*', ci.Length) : ci[..2] + new string('*', ci.Length - 4) + ci[^2..];

    /// <summary>Un productor solo ve los expedientes de sus predios (RNF-03).</summary>
    public IReadOnlyList<Expediente> Listar()
    {
        if (acceso.ProductorPropio is not int propio) return expedientes.Listar();

        var suyos = predios.Listar(p => p.ProductorId == propio).Select(p => p.Id).ToList();
        return expedientes.Listar(e => suyos.Contains(e.PredioId));
    }

    private bool EsPropio(Expediente e) =>
        acceso.ProductorPropio is not int propio || predios.Obtener(e.PredioId)?.ProductorId == propio;
}
