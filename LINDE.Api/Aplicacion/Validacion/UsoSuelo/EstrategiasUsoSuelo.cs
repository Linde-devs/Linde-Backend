using LINDE.Api.Dominio;

namespace LINDE.Api.Validacion.UsoSuelo;

public record EvaluacionUsoSuelo(EstadoUsoSuelo Estado, string Mensaje);

public interface IEstrategiaUsoSuelo
{
    string Nombre { get; }
    EvaluacionUsoSuelo Evaluar(ContextoValidacion contexto);
}

/// <summary>
/// El predio necesita autorización de la ABT. Revisa el POP y el Plan de desmonte registrados y, si el administrador
/// cargó la capa de autorizaciones de la ABT (CU-11), que el polígono esté cubierto por ellas (RF-07).
/// </summary>
public class EstrategiaAutorizacionAbt(IConsultaCapas? capas = null) : IEstrategiaUsoSuelo
{
    public string Nombre => "Autorización ABT";

    public EvaluacionUsoSuelo Evaluar(ContextoValidacion ctx)
    {
        var pop = ctx.Documentos.FirstOrDefault(d => d.Tipo == TipoDocumento.PlanOrdenamientoPredial);
        if (pop is null)
            return new(EstadoUsoSuelo.NoAutorizado,
                "El predio supera los límites de la Ley 741 y no tiene Plan de Ordenamiento Predial (POP).");

        if (pop.SuperficieHa is double autorizada && ctx.AreaPoligonoHa > autorizada)
            return new(EstadoUsoSuelo.NoAutorizado,
                $"El polígono ({ctx.AreaPoligonoHa:0.##} ha) supera la superficie del POP ({autorizada:0.##} ha) en {ctx.AreaPoligonoHa - autorizada:0.##} ha.");

        if (ctx.Predio.DesmonteDeclaradoHa is > 0 and var desmonte)
        {
            var plan = ctx.Documentos.FirstOrDefault(d => d.Tipo == TipoDocumento.PlanDesmonte);
            if (plan is null)
                return new(EstadoUsoSuelo.NoAutorizado,
                    $"Se declaran {desmonte:0.##} ha de desmonte sin Plan de Desmonte.");

            if (plan.SuperficieHa is double permitido && desmonte > permitido)
                return new(EstadoUsoSuelo.NoAutorizado,
                    $"El desmonte declarado ({desmonte:0.##} ha) supera el autorizado ({permitido:0.##} ha).");
        }

        if (capas is null || !capas.HayCapa(TipoCapa.AutorizacionesAbt))
            return new(EstadoUsoSuelo.Autorizado,
                "El uso del suelo está autorizado por el POP de la ABT. No hay capa de la ABT cargada: se comparó con los documentos registrados.");

        return CruzarConCapa(ctx, capas, pop);
    }

    /// <summary>RF-07 con la capa de la ABT: el polígono debe quedar cubierto por las áreas autorizadas.</summary>
    private static EvaluacionUsoSuelo CruzarConCapa(ContextoValidacion ctx, IConsultaCapas capas, Documento pop)
    {
        var vertices = ctx.Poligono.Vertices;
        if (ctx.Poligono.EsPunto)
            return capas.MejorCoincidencia(TipoCapa.AutorizacionesAbt, vertices) is { } c
                ? new(EstadoUsoSuelo.Autorizado, $"El punto está dentro de la autorización {c.Elemento.Codigo} de la capa de la ABT.")
                : new(EstadoUsoSuelo.NoAutorizado, "El punto no está dentro de ninguna autorización de la capa de la ABT.");

        var cubierta = capas.AreaCubiertaHa(TipoCapa.AutorizacionesAbt, vertices) ?? 0;
        var sinCubrir = Math.Max(0, ctx.AreaPoligonoHa - cubierta);
        var p = ctx.Parametros;
        var tolerancia = Math.Max(p.ToleranciaSuperposicionM2 / 10_000d, ctx.AreaPoligonoHa * p.ToleranciaSuperposicionPorcentaje / 100);
        if (sinCubrir > tolerancia)
            return new(EstadoUsoSuelo.NoAutorizado,
                $"{sinCubrir:0.##} ha del polígono no están cubiertas por ninguna autorización de la capa de la ABT.");

        var mejor = capas.MejorCoincidencia(TipoCapa.AutorizacionesAbt, vertices);
        if (mejor is not null && !string.IsNullOrWhiteSpace(pop.Numero) && !ValidadorTitularidad.MismoCodigo(pop.Numero, mejor.Elemento.Codigo))
            return new(EstadoUsoSuelo.NoAutorizado,
                $"Según la capa de la ABT, el predio está en la autorización {mejor.Elemento.Codigo}, pero el POP registrado es {pop.Numero}.");

        return new(EstadoUsoSuelo.Autorizado,
            $"El polígono está cubierto por la autorización {mejor?.Elemento.Codigo} de la capa de la ABT.");
    }
}

public class EstrategiaExencionLey741 : IEstrategiaUsoSuelo
{
    public string Nombre => "Exención Ley 741";

    public EvaluacionUsoSuelo Evaluar(ContextoValidacion ctx) =>
        new(EstadoUsoSuelo.NoRequiereAutorizacion,
            $"El predio está dentro de los límites de la Ley 741 ({ctx.Parametros.Ley741PredioMaximoHa:0.##} ha de predio " +
            $"o {ctx.Parametros.Ley741DesmonteMaximoHa:0.##} ha de desmonte); no requiere autorización.");
}

public class SelectorEstrategiaUsoSuelo(EstrategiaAutorizacionAbt abt, EstrategiaExencionLey741 exencion)
{
    public IEstrategiaUsoSuelo Seleccionar(ContextoValidacion ctx)
    {
        var p = ctx.Parametros;
        var superficie = ctx.AreaPoligonoHa > 0 ? ctx.AreaPoligonoHa : ctx.Predio.SuperficieDeclaradaHa;

        var predioPequeno = superficie <= p.Ley741PredioMaximoHa;
        var desmontePequeno = ctx.Predio.DesmonteDeclaradoHa is double d && d <= p.Ley741DesmonteMaximoHa;

        return predioPequeno || desmontePequeno ? exencion : abt;
    }
}
