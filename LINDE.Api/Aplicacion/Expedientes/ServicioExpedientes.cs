using LINDE.Api.Autenticacion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Expedientes;

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
