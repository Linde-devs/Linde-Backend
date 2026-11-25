using LINDE.Api.Dominio;
using LINDE.Api.Geometria;

namespace LINDE.Api.Expedientes;

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
