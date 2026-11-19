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

    public bool EsPunto => Vertices.Count == 1;

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
        Fecha = Fecha,
        Autor = Autor,
        Motivo = Motivo,
        Vigente = Vigente
    };
}
