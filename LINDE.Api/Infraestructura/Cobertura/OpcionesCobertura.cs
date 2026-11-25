namespace LINDE.Api.Cobertura;

public class OpcionesCobertura
{
    /// <summary>"Simulado" para desarrollo y pruebas; "EarthEngine" para usar el adaptador real.</summary>
    public string Proveedor { get; set; } = "Simulado";

    /// <summary>
    /// Ruta del archivo JSON con la llave de la cuenta de servicio. Va en dotnet user-secrets, nunca en el repositorio,
    /// y el archivo se guarda fuera de la carpeta del proyecto.
    /// </summary>
    public string ArchivoCredenciales { get; set; } = "";

    /// <summary>Proyecto de Google Cloud registrado en Earth Engine. Si queda vacío se usa el project_id de la llave.</summary>
    public string ProyectoGcp { get; set; } = "";

    public string UrlApi { get; set; } = "https://earthengine.googleapis.com/v1";

    /// <summary>Mapa de bosque del JRC al 31/12/2020, la referencia de la EUDR (banda Map: 1 = bosque).</summary>
    public string ColeccionReferencia { get; set; } = "JRC/GFC2020/V3";
    public string BandaReferencia { get; set; } = "Map";

    /// <summary>Dynamic World: cobertura del suelo cada 5 días con Sentinel-2 (banda label: 1 = árboles).</summary>
    public string ColeccionActual { get; set; } = "GOOGLE/DYNAMICWORLD/V1";

    /// <summary>Días hacia atrás que se juntan para la cobertura actual (la moda quita nubes y días sueltos).</summary>
    public int DiasVentanaActual { get; set; } = 180;

    /// <summary>Sentinel-2 en reflectancia de superficie, para las imágenes en color natural (RF-08).</summary>
    public string ColeccionImagenes { get; set; } = "COPERNICUS/S2_SR_HARMONIZED";

    /// <summary>RF-08 pide imágenes con nubosidad menor al 20 %.</summary>
    public int NubosidadMaximaPorcentaje { get; set; } = 20;

    public int DiasCache { get; set; } = 7;
    public int RetardoSimuladoMs { get; set; } = 800;
}
