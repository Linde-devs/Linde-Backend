namespace LINDE.Api.Dominio;

/// <summary>Estado del predio en el flujo TO-BE. Se guarda como valor; el comportamiento vive en Dominio/Estados.</summary>
public enum EstadoExpediente
{
    Registrado,
    ConAlertas,
    Aprobado
}

/// <summary>Resultado de la capa de uso permitido. El tercer estado evita marcar como infractores a quienes cumplen la Ley 741.</summary>
public enum EstadoUsoSuelo
{
    Autorizado,
    NoAutorizado,
    NoRequiereAutorizacion
}

public enum CapaValidacion
{
    Geometrica,
    Titularidad,
    UsoSuelo,
    Cobertura
}

/// <summary>Conforme: sin problemas. Alerta: el predio queda con alertas (informativas). Rechazado: el predio vuelve al productor.</summary>
public enum EstadoCapa
{
    Conforme,
    Alerta,
    Rechazado
}

public enum PrioridadAlerta
{
    Alta,
    Media,
    Baja
}

public enum TipoDocumento
{
    TituloPropiedad,
    PlanOrdenamientoPredial,
    PlanDesmonte
}
