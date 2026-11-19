using LINDE.Api.Auditoria;
using LINDE.Api.Cobertura;
using LINDE.Api.Configuracion;
using LINDE.Api.Datos;
using LINDE.Api.Expedientes;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;
using LINDE.Api.Validacion;
using LINDE.Api.Validacion.UsoSuelo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LINDE.Api.Infraestructura;

/// <summary>
/// Registro de dependencias. Aquí el contenedor de ASP.NET Core hace gran parte del trabajo de los patrones:
/// actúa como fábrica, mantiene el Singleton y conecta adaptadores, decoradores y proxies
/// sin que el resto del sistema lo note.
/// </summary>
public static class RegistroServicios
{
    public static IServiceCollection AgregarLinde(this IServiceCollection services, IConfiguration configuracion)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.Configure<OpcionesCobertura>(configuracion.GetSection("Cobertura"));

        // Datos en memoria (listas). Se reemplazarán por EF Core + PostGIS.
        services.AddSingleton(typeof(IRepositorio<>), typeof(RepositorioMemoria<>));
        services.AddSingleton<IRepositorioParametros, RepositorioParametrosMemoria>();

        // Auditoría
        services.AddScoped<IUsuarioActual, UsuarioActualHttp>();
        services.AddScoped<IBitacora, Bitacora>();
        services.AddScoped<AuditorOperaciones>();

        // Singleton por contenedor: parámetros (CU-12)
        services.AddSingleton<IServicioParametros, ServicioParametros>();
        services.AddScoped<AdministracionParametros>();
        services.AddScoped<IAdministracionParametros>(sp =>
            new AdministracionParametrosAuditada(sp.GetRequiredService<AdministracionParametros>(), sp.GetRequiredService<AuditorOperaciones>()));

        // Factory Method: lectores de polígono. Para agregar shapefile, se registra un lector más.
        services.AddSingleton<ILectorPoligono, LectorGeoJson>();
        services.AddSingleton<ILectorPoligono, LectorKml>();
        services.AddSingleton<FabricaLectores>();

        // Decorator: cada servicio real queda envuelto por su versión auditada.
        services.AddScoped<ServicioProductores>();
        services.AddScoped<ServicioPredios>();
        services.AddScoped<IServicioPredios>(sp =>
            new ServicioPrediosAuditado(sp.GetRequiredService<ServicioPredios>(), sp.GetRequiredService<AuditorOperaciones>()));
        services.AddScoped<ServicioPoligonos>();
        services.AddScoped<IServicioPoligonos>(sp =>
            new ServicioPoligonosAuditado(sp.GetRequiredService<ServicioPoligonos>(), sp.GetRequiredService<AuditorOperaciones>()));
        services.AddScoped<ServicioDocumentos>();
        services.AddScoped<IServicioDocumentos>(sp =>
            new ServicioDocumentosAuditado(sp.GetRequiredService<ServicioDocumentos>(),
                sp.GetRequiredService<IRepositorio<Dominio.Documento>>(), sp.GetRequiredService<AuditorOperaciones>()));

        // Adapter + Proxy: el validador de cobertura recibe IProveedorCobertura, que es el proxy de caché
        // delante del adaptador de Earth Engine (o de la simulación mientras no haya credenciales).
        services.AddHttpClient<EarthEngineAdapter>();
        services.AddSingleton<CoberturaSimulada>();
        services.AddScoped<IProveedorCobertura>(sp =>
        {
            var opciones = sp.GetRequiredService<IOptions<OpcionesCobertura>>().Value;
            IProveedorCobertura real = opciones.Proveedor.Equals("EarthEngine", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<EarthEngineAdapter>()
                : sp.GetRequiredService<CoberturaSimulada>();
            return new CoberturaCacheProxy(real, sp.GetRequiredService<IMemoryCache>(), TimeSpan.FromDays(opciones.DiasCache));
        });

        // Strategy: las dos estrategias de uso de suelo y el selector.
        services.AddScoped<EstrategiaAutorizacionAbt>();
        services.AddScoped<EstrategiaExencionLey741>();
        services.AddScoped<SelectorEstrategiaUsoSuelo>();

        // Factory Method por contenedor + Chain of Responsibility: los cuatro eslabones de la cadena.
        // Una quinta capa (por ejemplo, áreas protegidas) se agrega con una línea más.
        services.AddScoped<ValidadorCapa, ValidadorGeometrico>();
        services.AddScoped<ValidadorCapa, ValidadorTitularidad>();
        services.AddScoped<ValidadorCapa, ValidadorUsoSuelo>();
        services.AddScoped<ValidadorCapa, ValidadorCobertura>();

        // Facade: un solo punto de entrada para enviar a revisión.
        services.AddScoped<IServicioValidacionPredios, ServicioValidacionPredios>();

        // Builder del expediente (lo usa este servicio).
        services.AddScoped<ServicioExpedientes>();

        return services;
    }
}
