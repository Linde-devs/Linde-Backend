namespace LINDE.Api.Dominio;

/// <summary>La operación no está permitida en la situación actual (por ejemplo, por el estado del predio). Responde 409.</summary>
public class OperacionNoPermitidaException(string mensaje) : Exception(mensaje);

/// <summary>El recurso pedido no existe. Responde 404.</summary>
public class RecursoNoEncontradoException(string mensaje) : Exception(mensaje);

/// <summary>RNF-03. El usuario no puede ver ni modificar este recurso (por ejemplo, el predio de otro productor). Responde 403.</summary>
public class AccesoDenegadoException() : Exception("Acceso denegado");

/// <summary>Los datos enviados no son válidos. Responde 400. Campo indica qué dato falló, para marcarlo en el formulario.</summary>
public class DatosInvalidosException(string mensaje, string? campo = null) : ArgumentException(mensaje)
{
    public string? Campo { get; } = campo;
}
