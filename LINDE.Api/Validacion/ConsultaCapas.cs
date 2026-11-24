using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

namespace LINDE.Api.Validacion;

/// <summary>El elemento de una capa que más se superpone con el predio, y cuánto del predio cubre.</summary>
public record CoincidenciaCapa(ElementoCapa Elemento, double AreaComunHa, double AreaFueraHa);

/// <summary>
/// Cruce del predio con las capas oficiales (RF-07). Lo usan la capa de titularidad (INRA) y la estrategia de
/// autorización de la ABT. Si no hay capa vigente de ese tipo, devuelve null y el validador sigue con los
/// datos que declaró el productor, avisando que no se pudo cruzar.
/// </summary>
public interface IConsultaCapas
{
    bool HayCapa(TipoCapa tipo);

    /// <summary>El elemento con mayor área en común; null si no hay capa o si ninguno toca al predio.</summary>
    CoincidenciaCapa? MejorCoincidencia(TipoCapa tipo, IReadOnlyList<Coordenada> vertices);

    /// <summary>Hectáreas del predio cubiertas por la unión de todos los elementos de la capa; null si no hay capa.</summary>
    double? AreaCubiertaHa(TipoCapa tipo, IReadOnlyList<Coordenada> vertices);
}

public class ConsultaCapas(IRepositorio<CapaReferencia> capas, IRepositorio<ElementoCapa> elementos) : IConsultaCapas
{
    public bool HayCapa(TipoCapa tipo) => capas.Listar(c => c.Tipo == tipo && c.Vigente).Count > 0;

    public CoincidenciaCapa? MejorCoincidencia(TipoCapa tipo, IReadOnlyList<Coordenada> vertices)
    {
        if (!HayCapa(tipo)) return null;
        var (centro, predio, tocan) = Preparar(tipo, vertices);
        if (predio is null) return null;

        CoincidenciaCapa? mejor = null;
        foreach (var e in tocan)
        {
            var otro = Geometrias.Proyectar(e.Geometria, centro);
            var comun = predio is Point ? (otro.Covers(predio) ? 1d : 0d) : predio.Intersection(otro).Area;
            if (comun <= 0 || (mejor is not null && comun <= mejor.AreaComunHa * 10_000d)) continue;

            var fuera = predio is Point ? 0 : Math.Max(0, predio.Area - comun);
            mejor = new CoincidenciaCapa(e, predio is Point ? 0 : comun / 10_000d, fuera / 10_000d);
        }
        return mejor;
    }

    public double? AreaCubiertaHa(TipoCapa tipo, IReadOnlyList<Coordenada> vertices)
    {
        if (!HayCapa(tipo)) return null;
        var (centro, predio, tocan) = Preparar(tipo, vertices);
        if (predio is null || predio is Point || tocan.Count == 0) return 0;

        // Las autorizaciones pueden solaparse entre sí: se unen antes de medir, para no contar dos veces la misma área.
        var union = UnaryUnionOp.Union(tocan.Select(e => Geometrias.Proyectar(e.Geometria, centro)).ToList());
        return predio.Intersection(union).Area / 10_000d;
    }

    private (Coordenada Centro, Geometry? Predio, IReadOnlyList<ElementoCapa> Tocan) Preparar(
        TipoCapa tipo, IReadOnlyList<Coordenada> vertices)
    {
        if (vertices.Count == 0) return (default!, null, []);
        var centro = vertices.Count == 1 ? vertices[0] : Geometrias.Centro(vertices);

        // Con PostgreSQL se traduce a ST_Intersects con el índice GiST: la base devuelve solo los elementos cercanos.
        var geometria = Geometrias.Crear(vertices);
        var tocan = elementos.Listar(e => e.Tipo == tipo && e.Geometria.Intersects(geometria));

        Geometry predio = vertices.Count == 1
            ? new GeometryFactory().CreatePoint(new Coordinate(0, 0))
            : Geometrias.CrearProyectado(vertices, centro);
        return (centro, predio, tocan);
    }
}
