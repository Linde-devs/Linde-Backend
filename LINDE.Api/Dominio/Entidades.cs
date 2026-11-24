namespace LINDE.Api.Dominio;

/// <summary>Toda entidad guardada en un repositorio tiene un identificador numérico.</summary>
public interface IEntidad
{
    int Id { get; set; }
}

/// <summary>Coordenada geográfica en grados (WGS84). Igual que en GeoJSON: longitud primero, latitud después.</summary>
public readonly record struct Coordenada(double Lon, double Lat);

/// <summary>RF-01. Productor agrícola. Se registra junto con su cuenta de acceso (rol Productor).</summary>
public class Productor : IEntidad
{
    public int Id { get; set; }

    /// <summary>Código único asignado al registrarse, por ejemplo PRD-000001.</summary>
    public string Codigo { get; set; } = "";

    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";

    /// <summary>CI o NIT. No se puede repetir entre productores.</summary>
    public string Ci { get; set; } = "";

    /// <summary>Código del Registro Único Nacional de Productores Agropecuarios, si lo tiene.</summary>
    public string? Runpa { get; set; }

    public string Telefono { get; set; } = "";
    public string Municipio { get; set; } = "";

    /// <summary>Cuenta con la que el productor inicia sesión.</summary>
    public int UsuarioId { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// RF-14 (CU-09). Inspección de campo hecha por un técnico verificador. La registra el productor con lo que anotó el
/// técnico; LINDE no la valida: queda como evidencia en la Vista para revisión y en el expediente.
/// </summary>
public class InspeccionCampo : IEntidad
{
    public int Id { get; set; }
    public int PredioId { get; set; }

    /// <summary>Día de la visita al predio.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Nombre del técnico que hizo la inspección.</summary>
    public string Tecnico { get; set; } = "";

    public ResultadoInspeccion Resultado { get; set; }
    public string? Comentario { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    /// <summary>Correo de quien la registró en LINDE.</summary>
    public string RegistradaPor { get; set; } = "";
}

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

public class Alerta : IEntidad
{
    public int Id { get; set; }
    public int PredioId { get; set; }
    public CapaValidacion Capa { get; set; }
    public PrioridadAlerta Prioridad { get; set; }
    public string Mensaje { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public bool Resuelta { get; set; }
}

/// <summary>Resultado de una sola capa de validación.</summary>
public class ResultadoCapa
{
    public CapaValidacion Capa { get; init; }
    public EstadoCapa Estado { get; init; }
    public string Mensaje { get; init; } = "";

    /// <summary>Solo para la capa de uso permitido.</summary>
    public EstadoUsoSuelo? UsoSuelo { get; init; }
}

/// <summary>Resultado completo de un envío a revisión: una entrada por cada capa que se ejecutó.</summary>
public class ResultadoValidacion : IEntidad
{
    public int Id { get; set; }
    public int PredioId { get; set; }
    public int VersionPoligono { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public List<ResultadoCapa> Capas { get; set; } = new();

    /// <summary>True si la capa geométrica detuvo la cadena.</summary>
    public bool Cortado { get; set; }

    public EstadoExpediente EstadoResultante { get; set; }
    public string Mensaje { get; set; } = "";
}

/// <summary>Registro de auditoría: quién hizo qué, cuándo y con qué valores anteriores.</summary>
public class RegistroBitacora : IEntidad
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Usuario { get; set; } = "";
    public string Operacion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public string? EntidadId { get; set; }
    public string? ValoresAnteriores { get; set; }
    public string Resultado { get; set; } = "";
}

/// <summary>Parámetros del sistema (CU-12). Ningún umbral queda fijo en el código.</summary>
public sealed record Parametros
{
    public DateOnly FechaCorte { get; init; } = new(2020, 12, 31);
    public double UmbralPoligonoObligatorioHa { get; init; } = 4;
    public double ToleranciaSuperposicionM2 { get; init; } = 100;
    public double ToleranciaSuperposicionPorcentaje { get; init; } = 0.1;

    /// <summary>ERS RF-06: la superposición bloquea el envío. Decisión pendiente; se puede cambiar sin tocar el código.</summary>
    public bool SuperposicionBloquea { get; init; } = true;

    public double Ley741DesmonteMaximoHa { get; init; } = 20;
    public double Ley741PredioMaximoHa { get; init; } = 50;
    public double PerdidaCoberturaAlertaPorcentaje { get; init; } = 10;
    public double BufferMetros { get; init; } = 128;
}
