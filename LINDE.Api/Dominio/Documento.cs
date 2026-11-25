namespace LINDE.Api.Dominio;

public class Documento : IEntidad
{
    public int Id { get; set; }
    public int PredioId { get; set; }
    public TipoDocumento Tipo { get; set; }
    public string? Numero { get; set; }

    /// <summary>CI del titular que figura en el documento (se usa en la capa de titularidad).</summary>
    public string? TitularCi { get; set; }

    /// <summary>Superficie titulada o autorizada, en hectáreas.</summary>
    public double? SuperficieHa { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // RF-16: archivo escaneado del documento (PDF o imagen). Se guarda fuera de la base, en el almacén de archivos;
    // aquí queda la clave para encontrarlo y los datos para mostrarlo.

    /// <summary>Clave del archivo en el almacén. Null si todavía no se subió.</summary>
    public string? ArchivoClave { get; set; }

    /// <summary>Nombre original del archivo, para descargarlo con ese nombre.</summary>
    public string? ArchivoNombre { get; set; }

    public string? ArchivoTipo { get; set; }
    public long? ArchivoBytes { get; set; }
    public DateTime? ArchivoFecha { get; set; }

    public bool TieneArchivo => ArchivoClave is not null;
}
