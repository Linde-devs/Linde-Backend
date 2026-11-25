using LINDE.Api.Autenticacion;
using LINDE.Api.Configuracion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using LINDE.Api.Validacion.UsoSuelo;
using NetTopologySuite.Geometries;

namespace LINDE.Api.Validacion;

/// <summary>
/// Zona del predio para dibujar en el mapa. Geometria es un MultiPolygon en formato GeoJSON (longitud, latitud).
/// SuperaTolerancia indica si esa zona es la que generó la alerta o si es solo un margen de error del borde.
/// </summary>
public record ZonaVista(string Capa, string Titulo, double AreaHa, double Porcentaje, bool SuperaTolerancia, object Geometria);

/// <summary>
/// RF-07. "Muestra esas hectáreas resaltadas y el porcentaje fuera de autorización": calcula las partes del predio
/// que no cubre el título del INRA ni las autorizaciones de la ABT, con las mismas capas y tolerancia que las alertas.
/// La zona de la ABT solo se muestra si el predio necesita autorización: la decide la misma estrategia (Strategy)
/// que usa la capa de uso de suelo, así un predio exento por la Ley 741 no aparece como "sin autorización".
/// </summary>
public class ServicioZonasCapas(
    IRepositorio<Predio> predios,
    IRepositorio<Poligono> poligonos,
    IRepositorio<Documento> documentos,
    IServicioParametros parametros,
    IConsultaCapas capas,
    SelectorEstrategiaUsoSuelo selector,
    ControlAcceso acceso)
{
    public IReadOnlyList<ZonaVista> Calcular(int predioId)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        acceso.VerificarPredio(predio);
        var poligono = poligonos.Listar(p => p.PredioId == predioId && p.Vigente).FirstOrDefault();
        if (poligono is null || poligono.EsPunto) return [];

        var p = parametros.Actuales;
        var area = Geometrias.AreaHectareas(poligono.Vertices);
        var tolerancia = Math.Max(p.ToleranciaSuperposicionM2 / 10_000d, area * p.ToleranciaSuperposicionPorcentaje / 100);
        var zonas = new List<ZonaVista>();

        if (capas.ZonaFuera(TipoCapa.TitulosInra, poligono.Vertices) is { AreaHa: > 0 } inra)
            zonas.Add(Vista("TitulosInra",
                inra.Codigo is null ? "Fuera de los títulos del INRA" : $"Fuera del título {inra.Codigo} del INRA",
                inra, area, tolerancia));

        var contexto = new ContextoValidacion
        {
            Predio = predio, Poligono = poligono, Parametros = p, AreaPoligonoHa = area,
            Documentos = documentos.Listar(d => d.PredioId == predioId)
        };
        if (selector.Seleccionar(contexto) is EstrategiaAutorizacionAbt &&
            capas.ZonaFuera(TipoCapa.AutorizacionesAbt, poligono.Vertices) is { AreaHa: > 0 } abt)
            zonas.Add(Vista("AutorizacionesAbt", "Sin autorización de la ABT", abt, area, tolerancia));

        return zonas;
    }

    private static ZonaVista Vista(string capa, string titulo, ZonaSinCubrir z, double areaPredio, double tolerancia) =>
        new(capa, titulo, Math.Round(z.AreaHa, 4), Math.Round(z.AreaHa / areaPredio * 100, 2), z.AreaHa > tolerancia,
            GeoJson(z.Zona));

    /// <summary>Cualquier resultado de la diferencia (polígono, multipolígono o colección) como un MultiPolygon GeoJSON.</summary>
    public static object GeoJson(Geometry g)
    {
        var poligonos = new List<double[][][]>();
        for (var i = 0; i < g.NumGeometries; i++)
            if (g.GetGeometryN(i) is Polygon pol && !pol.IsEmpty)
                poligonos.Add(new[] { pol.ExteriorRing }.Concat(pol.InteriorRings)
                    .Select(anillo => anillo.Coordinates.Select(c => new[] { Math.Round(c.X, 7), Math.Round(c.Y, 7) }).ToArray())
                    .ToArray());
        return new { type = "MultiPolygon", coordinates = poligonos };
    }
}
