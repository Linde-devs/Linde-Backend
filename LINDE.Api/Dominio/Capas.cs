using NetTopologySuite.Geometries;

namespace LINDE.Api.Dominio;

/// <summary>Capas oficiales que carga el administrador (CU-11) y que usa el cruce de RF-07.</summary>
public enum TipoCapa
{
    /// <summary>Predios titulados del INRA: cada elemento es un título, con su número, el CI del titular y la superficie.</summary>
    TitulosInra,

    /// <summary>Áreas con autorización de la ABT (POP o Plan de desmonte): cada elemento es una autorización.</summary>
    AutorizacionesAbt
}

/// <summary>
/// Un archivo de capa cargado por el administrador. Solo una capa por tipo está vigente:
/// al cargar una nueva, la anterior queda en el historial y sus elementos se borran.
/// LINDE no se conecta en línea con el INRA ni con la ABT; sus datos entran como archivos.
/// </summary>
public class CapaReferencia : IEntidad
{
    public int Id { get; set; }
    public TipoCapa Tipo { get; set; }
    public string Nombre { get; set; } = "";

    /// <summary>De dónde salió el archivo (institución, fecha de corte o enlace), para la trazabilidad.</summary>
    public string Fuente { get; set; } = "";

    public string Archivo { get; set; } = "";
    public int CantidadElementos { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;
    public string CargadaPor { get; set; } = "";
    public bool Vigente { get; set; } = true;
}

/// <summary>Un polígono de una capa: un título del INRA o una autorización de la ABT.</summary>
public class ElementoCapa : IEntidad
{
    public int Id { get; set; }
    public int CapaId { get; set; }

    /// <summary>Copia del tipo de la capa, para filtrar sin unir tablas.</summary>
    public TipoCapa Tipo { get; set; }

    /// <summary>Número del título o de la autorización.</summary>
    public string Codigo { get; set; } = "";

    /// <summary>CI del titular (solo en la capa del INRA, si el archivo lo trae).</summary>
    public string? TitularCi { get; set; }

    /// <summary>Superficie que declara el archivo, en hectáreas (informativa; las áreas se calculan del polígono).</summary>
    public double? SuperficieHa { get; set; }

    /// <summary>Polígono o multipolígono en WGS84 (SRID 4326), con índice espacial en PostGIS.</summary>
    public Geometry Geometria { get; set; } = LINDE.Api.Geometria.Geometrias.FabricaWgs84.CreateGeometryCollection();
}
