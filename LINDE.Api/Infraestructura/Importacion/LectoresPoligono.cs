using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using LINDE.Api.Dominio;

namespace LINDE.Api.Importacion;

/// <summary>Interfaz común de lectura: el resto del sistema solo recibe una lista de coordenadas.</summary>
public interface ILectorPoligono
{
    string Formato { get; }
    bool Admite(string extension);
    IReadOnlyList<Coordenada> Leer(Stream contenido);
}

/// <summary>
/// Patrón Factory Method: entrega el lector adecuado según la extensión del archivo.
/// El servicio de polígonos pide un lector y nunca sabe cuál recibió.
/// Para aceptar shapefile basta con crear un lector nuevo y registrarlo en Program.cs.
/// </summary>
public class FabricaLectores(IEnumerable<ILectorPoligono> lectores)
{
    public ILectorPoligono CrearLector(string nombreArchivo)
    {
        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        return lectores.FirstOrDefault(l => l.Admite(extension))
               ?? throw new DatosInvalidosException(
                   $"El formato \"{extension}\" no está soportado. Formatos aceptados: {string.Join(", ", lectores.Select(l => l.Formato))}.");
    }
}

/// <summary>GeoJSON: acepta FeatureCollection, Feature, Polygon, MultiPolygon (primer polígono) o Point.</summary>
public class LectorGeoJson : ILectorPoligono
{
    public string Formato => "GeoJSON";

    public bool Admite(string extension) => extension is ".geojson" or ".json";

    public IReadOnlyList<Coordenada> Leer(Stream contenido)
    {
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(contenido);
        }
        catch (JsonException)
        {
            throw new DatosInvalidosException("El archivo GeoJSON no tiene un JSON válido.");
        }

        using (documento)
        {
            return LeerGeometria(BuscarGeometria(documento.RootElement));
        }
    }

    private static JsonElement BuscarGeometria(JsonElement elemento)
    {
        switch (Tipo(elemento))
        {
            case "FeatureCollection":
                var features = elemento.GetProperty("features");
                if (features.GetArrayLength() == 0)
                    throw new DatosInvalidosException("El GeoJSON no contiene ninguna geometría.");
                return BuscarGeometria(features[0]);
            case "Feature":
                return elemento.GetProperty("geometry");
            default:
                return elemento;
        }
    }

    private static IReadOnlyList<Coordenada> LeerGeometria(JsonElement geometria)
    {
        var coordenadas = geometria.GetProperty("coordinates");
        return Tipo(geometria) switch
        {
            "Point" => new[] { LeerPosicion(coordenadas) },
            "Polygon" => LeerAnillo(coordenadas[0]),
            "MultiPolygon" => LeerAnillo(coordenadas[0][0]),
            var otro => throw new DatosInvalidosException($"El tipo de geometría \"{otro}\" no es un polígono ni un punto.")
        };
    }

    private static IReadOnlyList<Coordenada> LeerAnillo(JsonElement anillo) =>
        anillo.EnumerateArray().Select(LeerPosicion).ToList();

    /// <summary>En GeoJSON el orden es longitud primero, latitud después.</summary>
    private static Coordenada LeerPosicion(JsonElement posicion) =>
        new(posicion[0].GetDouble(), posicion[1].GetDouble());

    private static string Tipo(JsonElement elemento) =>
        elemento.TryGetProperty("type", out var tipo)
            ? tipo.GetString() ?? ""
            : throw new DatosInvalidosException("El GeoJSON no indica el tipo (\"type\").");
}

/// <summary>KML: lee el primer elemento coordinates, cuyas tuplas son "longitud,latitud[,altitud]".</summary>
public class LectorKml : ILectorPoligono
{
    public string Formato => "KML";

    public bool Admite(string extension) => extension == ".kml";

    public IReadOnlyList<Coordenada> Leer(Stream contenido)
    {
        XDocument documento;
        try
        {
            documento = XDocument.Load(contenido);
        }
        catch (System.Xml.XmlException)
        {
            throw new DatosInvalidosException("El archivo KML no tiene un XML válido.");
        }

        var elemento = documento.Descendants().FirstOrDefault(e => e.Name.LocalName == "coordinates")
                       ?? throw new DatosInvalidosException("El KML no contiene coordenadas.");

        return elemento.Value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(LeerTupla)
            .ToList();
    }

    private static Coordenada LeerTupla(string tupla)
    {
        var partes = tupla.Split(',');
        if (partes.Length < 2 ||
            !double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
            !double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            throw new DatosInvalidosException($"La coordenada \"{tupla}\" del KML no es válida.");

        return new Coordenada(lon, lat);
    }
}
