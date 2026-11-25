using LINDE.Api.Auditoria;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Configuracion;

/// <summary>Lectura de los parámetros vigentes desde cualquier parte del sistema.</summary>
public interface IServicioParametros
{
    Parametros Actuales { get; }
    void Recargar();
}

/// <summary>
/// Patrón Singleton por contenedor: se registra con AddSingleton en Program.cs, no con una clase estática.
/// Mantiene los valores en memoria para que las validaciones no consulten la base cada vez,
/// y sigue siendo reemplazable en pruebas.
/// Un Singleton no debe depender de un servicio de vida corta (como el DbContext), por eso abre
/// un ámbito propio para leer el repositorio en lugar de recibirlo en el constructor.
/// </summary>
public class ServicioParametros : IServicioParametros
{
    private readonly IServiceScopeFactory _ambitos;
    private Parametros _actuales;

    public ServicioParametros(IServiceScopeFactory ambitos)
    {
        _ambitos = ambitos;
        _actuales = Leer();
    }

    public Parametros Actuales => Volatile.Read(ref _actuales);

    public void Recargar() => Volatile.Write(ref _actuales, Leer());

    private Parametros Leer()
    {
        using var ambito = _ambitos.CreateScope();
        return ambito.ServiceProvider.GetRequiredService<IRepositorioParametros>().Obtener();
    }
}

/// <summary>Modificación de parámetros por el administrador (CU-12).</summary>
public interface IAdministracionParametros
{
    Parametros Obtener();
    Parametros Actualizar(Parametros nuevos);
}

public class AdministracionParametros(IRepositorioParametros repositorio, IServicioParametros servicio) : IAdministracionParametros
{
    public Parametros Obtener() => repositorio.Obtener();

    public Parametros Actualizar(Parametros nuevos)
    {
        if (nuevos.UmbralPoligonoObligatorioHa <= 0 || nuevos.ToleranciaSuperposicionM2 < 0 ||
            nuevos.ToleranciaSuperposicionPorcentaje < 0 || nuevos.Ley741DesmonteMaximoHa < 0 ||
            nuevos.Ley741PredioMaximoHa < 0 || nuevos.PerdidaCoberturaAlertaPorcentaje <= 0 ||
            nuevos.BufferMetros < 0)
            throw new DatosInvalidosException("Los umbrales no pueden ser negativos y los obligatorios deben ser mayores a cero.");

        repositorio.Guardar(nuevos);
        servicio.Recargar();
        return nuevos;
    }
}

/// <summary>Patrón Decorator: agrega la bitácora sin que AdministracionParametros lo sepa.</summary>
public class AdministracionParametrosAuditada(IAdministracionParametros interno, AuditorOperaciones auditor) : IAdministracionParametros
{
    public Parametros Obtener() => interno.Obtener();

    public Parametros Actualizar(Parametros nuevos) =>
        auditor.Ejecutar("Actualizar parámetros", "Parametros", null,
            AuditorOperaciones.Instantanea(interno.Obtener()),
            () => interno.Actualizar(nuevos));
}
