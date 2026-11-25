using System.Text.Json.Serialization;
using LINDE.Api.Geometria;
using NetTopologySuite.Geometries;

namespace LINDE.Api.Dominio;

/// <summary>Patrón Prototype: un objeto que sabe crear una copia independiente de sí mismo.</summary>
public interface IPrototipo<out T>
{
    T Clonar();
}

/// <summary>
/// Una versión del polígono de un predio. Cada edición crea una versión nueva y la anterior
/// queda intacta en el historial (valor probatorio, RNF-05).
/// Un solo vértice representa un punto, permitido para predios de hasta 4 ha.
/// </summary>
public class Poligono : IEntidad, IPrototipo<Poligono>
{
    public int Id { get; set; }
    public int PredioId { get; set; }
    public int Version { get; set; } = 1;
    public List<Coordenada> Vertices { get; set; } = new();
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Autor { get; set; } = "";
    public string Motivo { get; set; } = "";
    public bool Vigente { get; set; } = true;

    /// <summary>
    /// RF-04. Precisión en metros que informó el GPS para cada vértice, en el mismo orden que Vertices. Un null es un
    /// vértice marcado a mano o corregido; la lista queda vacía si el polígono vino de un archivo.
    /// </summary>
    public List<double?> PrecisionesM { get; set; } = new();

    public bool EsPunto => Vertices.Count == 1;

    /// <summary>
    /// La misma geometría, como la guarda PostGIS: columna geometry en WGS84 (SRID 4326) con índice espacial.
    /// No tiene valor propio: se arma a partir de Vertices al guardar y los rellena al leer.
    /// Así el resto del sistema sigue trabajando con la lista de vértices, y la base puede usar ST_Intersects (RF-06).
    /// </summary>
    [JsonIgnore]
    public Geometry Geometria
    {
        get => Geometrias.CrearParaGuardar(Vertices);
        set => Vertices = Geometrias.Vertices(value);
    }

    /// <summary>
    /// Copia profunda: la lista de vértices es nueva. Con una copia superficial, las dos versiones
    /// compartirían la misma lista y editar una alteraría la otra.
    /// </summary>
    public Poligono Clonar() => new()
    {
        Id = 0,
        PredioId = PredioId,
        Version = Version,
        Vertices = new List<Coordenada>(Vertices),
        PrecisionesM = new List<double?>(PrecisionesM),
        Fecha = Fecha,
        Autor = Autor,
        Motivo = Motivo,
        Vigente = Vigente
    };
}
