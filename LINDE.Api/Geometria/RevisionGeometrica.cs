using LINDE.Api.Dominio;
using NetTopologySuite.Operation.Valid;

namespace LINDE.Api.Geometria;

/// <summary>Tipos de error que RF-03 exige informar al rechazar un polígono.</summary>
public enum ErrorGeometria
{
    SinVertices,
    FueraDeWgs84,
    FueraDeBolivia,
    PocosVertices,
    AnilloNoCerrado,
    VerticesDuplicados,
    Autointerseccion,
    GeometriaNoValida
}

public sealed record ProblemaGeometria(ErrorGeometria Tipo, string Mensaje)
{
    /// <summary>Nombre del tipo de error tal como lo ve el usuario.</summary>
    public string Titulo => Tipo switch
    {
        ErrorGeometria.SinVertices => "Sin vértices",
        ErrorGeometria.FueraDeWgs84 => "Coordenadas fuera de WGS84",
        ErrorGeometria.FueraDeBolivia => "Coordenadas fuera de Bolivia",
        ErrorGeometria.PocosVertices => "Menos de 4 vértices",
        ErrorGeometria.AnilloNoCerrado => "Anillo no cerrado",
        ErrorGeometria.VerticesDuplicados => "Vértices duplicados",
        ErrorGeometria.Autointerseccion => "Autointersección",
        _ => "Geometría no válida"
    };
}

/// <summary>
/// Revisión geométrica de un polígono o un punto en WGS84 (EPSG:4326), dentro de Bolivia. La usan la carga del polígono (RF-03),
/// para rechazarlo apenas se sube, y la capa geométrica de la cadena de validación, al enviarlo a revisión.
/// No revisa la superposición con otros predios: eso lo hace la capa geométrica (RF-06).
/// </summary>
public static class RevisionGeometrica
{
    /// <summary>Devuelve el primer problema encontrado o null si la geometría es válida.</summary>
    public static ProblemaGeometria? Revisar(IReadOnlyList<Coordenada> v)
    {
        if (v.Count == 0)
            return new(ErrorGeometria.SinVertices, "El archivo no contiene ningún vértice.");

        var indiceFuera = IndiceFueraDeRango(v);
        if (indiceFuera >= 0)
            return new(ErrorGeometria.FueraDeWgs84,
                $"La coordenada ({v[indiceFuera].Lon}, {v[indiceFuera].Lat}) no está en grados WGS84 (EPSG:4326). " +
                "La longitud debe estar entre -180 y 180 y la latitud entre -90 y 90; revisa que el archivo no esté en UTM.");

        // Bolivia no tiene mar: una coordenada fuera del país casi siempre es un error de carga
        // (latitud y longitud invertidas, un signo olvidado o un archivo de otro lugar).
        var indiceFueraDeBolivia = IndiceFueraDeBolivia(v);
        if (indiceFueraDeBolivia >= 0)
        {
            var c = v[indiceFueraDeBolivia];
            var cual = v.Count == 1 ? "El punto" : $"El vértice {indiceFueraDeBolivia + 1}";
            return new(ErrorGeometria.FueraDeBolivia,
                $"{cual} (lat. {c.Lat}, long. {c.Lon}) está fuera de Bolivia. Revisa que la latitud y la longitud " +
                "no estén invertidas y que las dos sean negativas (por ejemplo, lat. -17.78 y long. -63.18 en Santa Cruz).");
        }

        // Un solo vértice es un punto, permitido para predios de hasta 4 ha (RF-05, se revisa al enviar).
        if (v.Count == 1)
            return null;

        // Un anillo GeoJSON necesita 4 posiciones o más: 3 vértices distintos y el cierre.
        if (v.Count < 4)
            return new(ErrorGeometria.PocosVertices, "El polígono necesita al menos 3 vértices distintos.");

        if (v[0] != v[^1])
            return new(ErrorGeometria.AnilloNoCerrado, "El polígono no está cerrado: el último vértice debe repetir el primero.");

        var sinCierre = v.Take(v.Count - 1).ToList();
        if (sinCierre.Distinct().Count() != sinCierre.Count)
            return new(ErrorGeometria.VerticesDuplicados, "El polígono tiene vértices duplicados.");

        var validacion = new IsValidOp(Geometrias.Crear(v));
        if (!validacion.IsValid)
        {
            var error = validacion.ValidationError;
            return error.ErrorType is TopologyValidationErrors.SelfIntersection or TopologyValidationErrors.RingSelfIntersection
                ? new(ErrorGeometria.Autointerseccion, "Los lados del polígono se cruzan entre sí.")
                : new(ErrorGeometria.GeometriaNoValida, $"La geometría no es válida ({error.Message}).");
        }

        return null;
    }

    private static int IndiceFueraDeRango(IReadOnlyList<Coordenada> v)
    {
        for (var i = 0; i < v.Count; i++)
            if (!EnRango(v[i])) return i;
        return -1;
    }

    private static int IndiceFueraDeBolivia(IReadOnlyList<Coordenada> v)
    {
        for (var i = 0; i < v.Count; i++)
            if (!LimiteBolivia.Contiene(v[i])) return i;
        return -1;
    }

    private static bool EnRango(Coordenada c) =>
        double.IsFinite(c.Lon) && double.IsFinite(c.Lat) && c.Lon is >= -180 and <= 180 && c.Lat is >= -90 and <= 90;
}

/// <summary>Polígono rechazado al cargarlo. Responde 400 e indica el tipo de error.</summary>
public class GeometriaInvalidaException(ProblemaGeometria problema)
    : DatosInvalidosException($"{problema.Titulo}: {problema.Mensaje}", "geometria")
{
    public ErrorGeometria Tipo { get; } = problema.Tipo;
}
