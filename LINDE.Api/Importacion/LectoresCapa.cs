using System.Globalization;
using System.Text.Json;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using NetTopologySuite.Geometries;

namespace LINDE.Api.Importacion;

/// <summary>Un polígono leído de un archivo de capa, con los datos que lo identifican.</summary>
public record ElementoLeido(Geometry Geometria, string Codigo, string? TitularCi, double? SuperficieHa);

/// <summary>
/// Lector de archivos de capa (CU-11). A diferencia de ILectorPoligono, devuelve muchos polígonos con sus atributos.
/// </summary>
public interface ILectorCapa
{
    string Formato { get; }
    bool Admite(string extension);
    IReadOnlyList<ElementoLeido> Leer(Stream contenido);
}

/// <summary>
/// Patrón Factory Method, igual que FabricaLectores en CU-04: entrega el lector según la extensión.
/// Para aceptar shapefile (el formato habitual del INRA y la ABT) basta con crear un lector más y registrarlo.
/// </summary>
public class FabricaLectoresCapa(IEnumerable<ILectorCapa> lectores)
{
    public ILectorCapa CrearLector(string nombreArchivo)
    {
        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        return lectores.FirstOrDefault(l => l.Admite(extension))
               ?? throw new DatosInvalidosException(
                   $"El formato \"{extension}\" no está soportado para capas. Formatos aceptados: {string.Join(", ", lectores.Select(l => l.Formato))}.");
    }
}

/// <summary>
/// GeoJSON con una FeatureCollection de polígonos o multipolígonos. Cada Feature es un título o una autorización.
/// Los atributos se buscan con varios nombres habituales (por ejemplo "codigo", "numero" o "nro_titulo").
/// </summary>
public class LectorCapaGeoJson : ILectorCapa
{
    private static readonly string[] ClavesCodigo = ["codigo", "numero", "nro_titulo", "titulo", "nro", "cod", "id"];
    private static readonly string[] ClavesCi = ["titular_ci", "ci_titular", "ci", "titularci"];
    private static readonly string[] ClavesSuperficie = ["superficie_ha", "sup_ha", "superficie", "area_ha", "superficieha"];

    public string Formato => "GeoJSON";

    public bool Admite(string extension) => extension is ".geojson" or ".json";

    public IReadOnlyList<ElementoLeido> Leer(Stream contenido)
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
            var raiz = documento.RootElement;
            if (!raiz.TryGetProperty("type", out var tipo) || tipo.GetString() != "FeatureCollection" ||
                !raiz.TryGetProperty("features", out var features))
                throw new DatosInvalidosException("La capa debe ser un GeoJSON de tipo FeatureCollection.");

            var elementos = new List<ElementoLeido>();
            var numero = 0;
            foreach (var feature in features.EnumerateArray())
            {
                numero++;
                elementos.Add(LeerFeature(feature, numero));
            }

            if (elementos.Count == 0)
                throw new DatosInvalidosException("La capa no contiene ningún polígono.");
            return elementos;
        }
    }

    private static ElementoLeido LeerFeature(JsonElement feature, int numero)
    {
        if (!feature.TryGetProperty("geometry", out var geometria) || geometria.ValueKind != JsonValueKind.Object)
            throw new DatosInvalidosException($"El elemento {numero} de la capa no tiene geometría.");

        var geo = LeerGeometria(geometria, numero);
        if (!geo.IsValid)
            throw new DatosInvalidosException($"El polígono del elemento {numero} no es válido (lados cruzados o anillo abierto).");
        if (geo.Coordinates.Any(c => c.X is < -180 or > 180 || c.Y is < -90 or > 90))
            throw new DatosInvalidosException($"El elemento {numero} tiene coordenadas fuera de WGS84 (longitud primero, latitud después).");

        var propiedades = feature.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
        var codigo = Texto(propiedades, ClavesCodigo);
        if (string.IsNullOrWhiteSpace(codigo))
            throw new DatosInvalidosException(
                $"El elemento {numero} no tiene número de título o de autorización (atributo \"codigo\" o \"numero\").");

        return new ElementoLeido(geo, codigo.Trim(), Texto(propiedades, ClavesCi)?.Trim(), Numero(propiedades, ClavesSuperficie));
    }

    private static Geometry LeerGeometria(JsonElement geometria, int numero)
    {
        var tipo = geometria.TryGetProperty("type", out var t) ? t.GetString() : null;
        var coordenadas = geometria.GetProperty("coordinates");
        var fabrica = Geometrias.FabricaWgs84;
        try
        {
            return tipo switch
            {
                "Polygon" => Poligono(coordenadas, fabrica),
                "MultiPolygon" => fabrica.CreateMultiPolygon(coordenadas.EnumerateArray().Select(c => Poligono(c, fabrica)).ToArray()),
                _ => throw new DatosInvalidosException($"El elemento {numero} es de tipo \"{tipo}\"; la capa solo admite polígonos.")
            };
        }
        catch (ArgumentException e) when (e is not DatosInvalidosException)
        {
            throw new DatosInvalidosException($"El polígono del elemento {numero} no está cerrado o tiene menos de 4 posiciones.");
        }
    }

    private static Polygon Poligono(JsonElement anillos, GeometryFactory fabrica)
    {
        LinearRing Anillo(JsonElement a) => fabrica.CreateLinearRing(
            a.EnumerateArray().Select(c => new Coordinate(c[0].GetDouble(), c[1].GetDouble())).ToArray());
        var lista = anillos.EnumerateArray().ToList();
        return fabrica.CreatePolygon(Anillo(lista[0]), lista.Skip(1).Select(Anillo).ToArray());
    }

    private static string? Texto(JsonElement propiedades, string[] claves)
    {
        var valor = Buscar(propiedades, claves);
        return valor?.ValueKind switch
        {
            JsonValueKind.String => valor.Value.GetString(),
            JsonValueKind.Number => valor.Value.GetRawText(),
            _ => null
        };
    }

    private static double? Numero(JsonElement propiedades, string[] claves)
    {
        var valor = Buscar(propiedades, claves);
        if (valor?.ValueKind == JsonValueKind.Number) return valor.Value.GetDouble();
        if (valor?.ValueKind == JsonValueKind.String &&
            double.TryParse(valor.Value.GetString()?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            return n;
        return null;
    }

    private static JsonElement? Buscar(JsonElement propiedades, string[] claves)
    {
        if (propiedades.ValueKind != JsonValueKind.Object) return null;
        foreach (var propiedad in propiedades.EnumerateObject())
            if (claves.Contains(propiedad.Name.ToLowerInvariant()))
                return propiedad.Value;
        return null;
    }
}
