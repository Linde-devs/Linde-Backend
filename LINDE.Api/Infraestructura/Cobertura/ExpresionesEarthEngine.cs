using System.Text.Json.Nodes;
using LINDE.Api.Dominio;

namespace LINDE.Api.Cobertura;

/// <summary>
/// Arma las expresiones de Earth Engine en el formato JSON de su API REST: cada paso es una función del servidor
/// ("functionInvocationValue") con sus argumentos, y los valores fijos van como "constantValue".
/// Equivale, por ejemplo, a ee.Image('JRC/GFC2020/V3').select('Map').unmask(0).reduceRegion(...) en Python.
/// </summary>
public static class ExpresionesEarthEngine
{
    public static JsonObject Constante(JsonNode? valor) => new() { ["constantValue"] = valor };

    public static JsonObject Funcion(string nombre, params (string Nombre, JsonObject Valor)[] argumentos)
    {
        var args = new JsonObject();
        // Un nodo JSON solo puede estar en un lugar del árbol: si ya se usó (la geometría aparece en varias consultas),
        // se copia.
        foreach (var (n, v) in argumentos) args[n] = v.Parent is null ? v : v.DeepClone();
        return new JsonObject { ["functionInvocationValue"] = new JsonObject { ["functionName"] = nombre, ["arguments"] = args } };
    }

    /// <summary>Cuerpo de value:compute: el grafo con un único nodo "0", que es el resultado.</summary>
    public static JsonObject Solicitud(JsonObject expresion) => new()
    {
        ["expression"] = new JsonObject { ["result"] = "0", ["values"] = new JsonObject { ["0"] = expresion } }
    };

    /// <summary>Polígono en WGS84 con el anillo cerrado, en orden longitud y latitud.</summary>
    public static JsonObject Poligono(IReadOnlyList<Coordenada> vertices)
    {
        var anillo = new JsonArray();
        foreach (var v in vertices) anillo.Add(new JsonArray(v.Lon, v.Lat));
        if (vertices.Count > 0 && vertices[0] != vertices[^1]) anillo.Add(new JsonArray(vertices[0].Lon, vertices[0].Lat));
        return Funcion("GeometryConstructors.Polygon", ("coordinates", Constante(new JsonArray(anillo))), ("evenOdd", Constante(true)));
    }

    private static JsonObject Fecha(DateOnly fecha) => Funcion("Date", ("value", Constante(fecha.ToString("yyyy-MM-dd"))));

    /// <summary>ee.ImageCollection(id).filterDate(inicio, fin)</summary>
    public static JsonObject ColeccionEntre(string coleccion, DateOnly inicio, DateOnly fin) =>
        Funcion("Collection.filter",
            ("collection", Funcion("ImageCollection.load", ("id", Constante(coleccion)))),
            ("filter", Funcion("Filter.dateRangeContains",
                ("leftValue", Funcion("DateRange", ("start", Fecha(inicio)), ("end", Fecha(fin)))),
                ("rightField", Constante("system:time_start")))));

    /// <summary>Media de una imagen de una banda dentro del polígono: con 0 y 1, es la fracción del área.</summary>
    private static JsonObject Media(JsonObject imagen, JsonObject geometria, int escala) =>
        Funcion("Image.reduceRegion",
            ("image", imagen),
            ("reducer", Funcion("Reducer.mean")),
            ("geometry", geometria),
            ("scale", Constante(escala)),
            ("maxPixels", Constante(1e10)),
            ("bestEffort", Constante(true)));

    private static JsonObject Seleccionar(JsonObject imagen, params string[] bandas) =>
        Funcion("Image.select", ("input", imagen), ("bandSelectors", Constante(new JsonArray(bandas.Select(b => (JsonNode)b).ToArray()))));

    /// <summary>ee.Image.constant(valor): el servidor exige una imagen donde Python acepta un número suelto.</summary>
    private static JsonObject ImagenConstante(double valor) => Funcion("Image.constant", ("value", Constante(valor)));

    /// <summary>ee.Image(id).select(banda).unmask(0): el mapa solo marca el bosque (1); lo demás pasa a 0.</summary>
    public static JsonObject FraccionReferencia(string imagen, string banda, JsonObject geometria, int escala) =>
        Media(Funcion("Image.unmask",
                ("input", Seleccionar(Funcion("Image.load", ("id", Constante(imagen))), banda)),
                ("value", ImagenConstante(0))),
            geometria, escala);

    /// <summary>recientes.mode().select('label_mode').eq(1): clase más frecuente de cada píxel y si es "árboles".</summary>
    public static JsonObject FraccionArbolesActual(JsonObject recientes, JsonObject geometria, int escala) =>
        Media(Funcion("Image.eq",
                ("image1", Seleccionar(Funcion("ImageCollection.reduce", ("collection", recientes), ("reducer", Funcion("Reducer.mode"))), "label_mode")),
                ("image2", ImagenConstante(1))),
            geometria, escala);

    /// <summary>
    /// recientes.filterBounds(geometría).aggregate_max('system:time_start'). Con conFeature la geometría va dentro de
    /// un Feature, como la envía ee.Filter.bounds en JavaScript.
    /// </summary>
    public static JsonObject FechaMasReciente(JsonObject recientes, JsonObject geometria, bool conFeature = false) =>
        Funcion("AggregateFeatureCollection.max",
            ("collection", Funcion("Collection.filter",
                ("collection", recientes),
                ("filter", Funcion("Filter.intersects", ("leftField", Constante(".all")),
                    ("rightValue", conFeature ? Funcion("Feature", ("geometry", geometria)) : geometria))))),
            ("property", Constante("system:time_start")));

    /// <summary>
    /// RF-08. ee.ImageCollection(id).filterDate(inicio, fin).filter(ee.Filter.lt('CLOUDY_PIXEL_PERCENTAGE', n))
    /// .median().select(['B4','B3','B2']).visualize({min: 0, max: 3000}): rojo, verde y azul en 8 bits.
    /// </summary>
    public static JsonObject ColorNatural(string coleccion, DateOnly inicio, DateOnly fin, int nubosidadMaxima) =>
        Funcion("Image.visualize",
            ("image", Seleccionar(
                Funcion("ImageCollection.reduce",
                    ("collection", Funcion("Collection.filter",
                        ("collection", ColeccionEntre(coleccion, inicio, fin)),
                        ("filter", Funcion("Filter.lessThan",
                            ("leftField", Constante("CLOUDY_PIXEL_PERCENTAGE")), ("rightValue", Constante(nubosidadMaxima)))))),
                    ("reducer", Funcion("Reducer.median"))),
                "B4_median", "B3_median", "B2_median")),
            ("min", Constante(0)),
            ("max", Constante(3000)));
}
