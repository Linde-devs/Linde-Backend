using System.Text.Json;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Auditoria;

/// <summary>Usuario que hace la petición, tomado del token de la sesión.</summary>
public interface IUsuarioActual
{
    string Nombre { get; }
}

/// <summary>Para la bitácora se usa el correo, que es único; el nombre puede repetirse.</summary>
public class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    public string Nombre =>
        accesor.HttpContext?.User.FindFirst(LINDE.Api.Autenticacion.Claims.Correo)?.Value ?? "anonimo";
}

public interface IBitacora
{
    void Registrar(string operacion, string entidad, string? entidadId, string? valoresAnteriores, string resultado);
    IReadOnlyList<RegistroBitacora> Listar(string? entidad = null, string? entidadId = null);
}

public class Bitacora(IRepositorio<RegistroBitacora> repositorio, IUsuarioActual usuario) : IBitacora
{
    public void Registrar(string operacion, string entidad, string? entidadId, string? valoresAnteriores, string resultado) =>
        repositorio.Agregar(new RegistroBitacora
        {
            Usuario = usuario.Nombre,
            Operacion = operacion,
            Entidad = entidad,
            EntidadId = entidadId,
            ValoresAnteriores = valoresAnteriores,
            Resultado = resultado
        });

    public IReadOnlyList<RegistroBitacora> Listar(string? entidad = null, string? entidadId = null) =>
        repositorio.Listar(r =>
                (entidad is null || r.Entidad == entidad) &&
                (entidadId is null || r.EntidadId == entidadId))
            .OrderByDescending(r => r.Fecha)
            .ToList();
}

/// <summary>
/// Lo que comparten todos los decoradores de auditoría: registrar la operación, el usuario,
/// los valores anteriores y si terminó bien o fue rechazada.
/// </summary>
public class AuditorOperaciones(IBitacora bitacora)
{
    private static readonly JsonSerializerOptions Opciones = new() { WriteIndented = false };

    public static string? Instantanea(object? valor) =>
        valor is null ? null : JsonSerializer.Serialize(valor, valor.GetType(), Opciones);

    public T Ejecutar<T>(string operacion, string entidad, string? entidadId, string? valoresAnteriores,
        Func<T> accion, Func<T, string?>? idResultado = null)
    {
        try
        {
            var resultado = accion();
            bitacora.Registrar(operacion, entidad, idResultado?.Invoke(resultado) ?? entidadId, valoresAnteriores, "Correcto");
            return resultado;
        }
        catch (Exception ex)
        {
            bitacora.Registrar(operacion, entidad, entidadId, valoresAnteriores, $"Rechazado: {ex.Message}");
            throw;
        }
    }

    public void Ejecutar(string operacion, string entidad, string? entidadId, string? valoresAnteriores, Action accion) =>
        Ejecutar<object?>(operacion, entidad, entidadId, valoresAnteriores, () =>
        {
            accion();
            return null;
        });
}
