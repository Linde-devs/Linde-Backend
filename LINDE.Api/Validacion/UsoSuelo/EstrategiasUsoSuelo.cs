using LINDE.Api.Dominio;

namespace LINDE.Api.Validacion.UsoSuelo;

public record EvaluacionUsoSuelo(EstadoUsoSuelo Estado, string Mensaje);

public interface IEstrategiaUsoSuelo
{
    string Nombre { get; }
    EvaluacionUsoSuelo Evaluar(ContextoValidacion contexto);
}

public class EstrategiaAutorizacionAbt : IEstrategiaUsoSuelo
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

        return new(EstadoUsoSuelo.Autorizado, "El uso del suelo está autorizado por el POP de la ABT.");
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
