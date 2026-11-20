using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LINDE.Api.Dominio;
using NetTopologySuite.Geometries;

namespace LINDE.Api.Geometria;

/// <summary>
/// Utilidades geométricas sobre NetTopologySuite (la misma librería que usa EF Core con PostGIS).
/// Las áreas se calculan en metros sobre una proyección, nunca en grados (RNF-09).
/// </summary>
public static class Geometrias
{
    /// <summary>SRID 4326 = WGS84, coordenadas en grados.</summary>
    public static readonly GeometryFactory FabricaWgs84 = new(new PrecisionModel(), 4326);

    private static readonly GeometryFactory FabricaPlana = new(new PrecisionModel(), 0);

    /// <summary>Crea un punto (un vértice) o un polígono (anillo cerrado) en grados.</summary>
    public static Geometry Crear(IReadOnlyList<Coordenada> vertices)
    {
        if (vertices.Count == 1)
            return FabricaWgs84.CreatePoint(new Coordinate(vertices[0].Lon, vertices[0].Lat));

        return FabricaWgs84.CreatePolygon(vertices.Select(v => new Coordinate(v.Lon, v.Lat)).ToArray());
    }

    /// <summary>
    /// Versión segura de Crear para guardar en la base: si los vértices todavía no forman un polígono
    /// (por ejemplo, un anillo abierto), devuelve una geometría vacía en lugar de lanzar una excepción.
    /// La revisión geométrica impide que algo así se guarde; esto solo evita errores al comparar cambios.
    /// </summary>
    public static Geometry CrearParaGuardar(IReadOnlyList<Coordenada> vertices)
    {
        if (vertices.Count == 1 || (vertices.Count >= 4 && vertices[0] == vertices[^1]))
            return Crear(vertices);
        return FabricaWgs84.CreateGeometryCollection();
    }

    /// <summary>Vértices de una geometría leída de la base: el punto, o el anillo exterior del polígono.</summary>
    public static List<Coordenada> Vertices(Geometry? geometria) => geometria switch
    {
        Point p when !p.IsEmpty => [new Coordenada(p.X, p.Y)],
        Polygon p when !p.IsEmpty => p.ExteriorRing.Coordinates.Select(c => new Coordenada(c.X, c.Y)).ToList(),
        _ => []
    };

    /// <summary>Crea el polígono proyectado en metros, centrado en <paramref name="centro"/>.</summary>
    public static Polygon CrearProyectado(IReadOnlyList<Coordenada> vertices, Coordenada centro)
    {
        var proyeccion = new ProyeccionAreaEquivalente(centro);
        var coordenadas = vertices.Select(v =>
        {
            var (x, y) = proyeccion.Proyectar(v);
            return new Coordinate(x, y);
        }).ToArray();
        return FabricaPlana.CreatePolygon(coordenadas);
    }

    /// <summary>Centro aproximado del polígono (promedio de vértices sin repetir el de cierre).</summary>
    public static Coordenada Centro(IReadOnlyList<Coordenada> vertices)
    {
        var distintos = vertices.Count > 1 && vertices[0] == vertices[^1]
            ? vertices.Take(vertices.Count - 1).ToList()
            : vertices.ToList();
        return new Coordenada(distintos.Average(v => v.Lon), distintos.Average(v => v.Lat));
    }

    /// <summary>Área del polígono en hectáreas.</summary>
    public static double AreaHectareas(IReadOnlyList<Coordenada> vertices) =>
        CrearProyectado(vertices, Centro(vertices)).Area / 10_000d;

    /// <summary>
    /// Huella de la geometría: cambia si se mueve cualquier vértice. La usa el proxy de caché
    /// para saber si un análisis satelital anterior sigue sirviendo.
    /// </summary>
    public static string Huella(IReadOnlyList<Coordenada> vertices)
    {
        var texto = string.Join(";", vertices.Select(v =>
            v.Lon.ToString("F7", CultureInfo.InvariantCulture) + "," + v.Lat.ToString("F7", CultureInfo.InvariantCulture)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }
}

/// <summary>
/// Proyección azimutal equivalente de Lambert (esférica, radio auténtico), centrada en el predio.
/// Conserva las áreas, que es lo que necesitan la superficie y la superposición.
/// Si el equipo confirma UTM 20S (EPSG:32720), se reemplaza esta clase sin tocar las validaciones.
/// </summary>
public sealed class ProyeccionAreaEquivalente
{
    private const double RadioAutentico = 6_371_007.181;
    private readonly double _lon0;
    private readonly double _sinLat0;
    private readonly double _cosLat0;

    public ProyeccionAreaEquivalente(Coordenada centro)
    {
        _lon0 = GradosARadianes(centro.Lon);
        var lat0 = GradosARadianes(centro.Lat);
        _sinLat0 = Math.Sin(lat0);
        _cosLat0 = Math.Cos(lat0);
    }

    public (double X, double Y) Proyectar(Coordenada c)
    {
        var lat = GradosARadianes(c.Lat);
        var dLon = GradosARadianes(c.Lon) - _lon0;
        var sinLat = Math.Sin(lat);
        var cosLat = Math.Cos(lat);
        var cosDLon = Math.Cos(dLon);

        var k = Math.Sqrt(2 / (1 + _sinLat0 * sinLat + _cosLat0 * cosLat * cosDLon));
        var x = RadioAutentico * k * cosLat * Math.Sin(dLon);
        var y = RadioAutentico * k * (_cosLat0 * sinLat - _sinLat0 * cosLat * cosDLon);
        return (x, y);
    }

    private static double GradosARadianes(double grados) => grados * Math.PI / 180d;
}
