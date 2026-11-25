using LINDE.Api.Auditoria;
using LINDE.Api.Cobertura;
using LINDE.Api.Configuracion;
using LINDE.Api.Datos;
using LINDE.Api.Expedientes;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;
using LINDE.Api.Validacion;
using LINDE.Api.Validacion.UsoSuelo;
using Microsoft.EntityFrameworkCore;
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

        // Datos: PostgreSQL + PostGIS con EF Core, o listas en memoria si "Datos:Proveedor" es "Memoria".
        if (UsaMemoria(configuracion))
        {
            services.AddSingleton(typeof(IRepositorio<>), typeof(RepositorioMemoria<>));
            services.AddSingleton<IRepositorioParametros, RepositorioParametrosMemoria>();
        }
        else
        {
            var conexion = configuracion.GetConnectionString("Linde")
                ?? throw new InvalidOperationException(
                    "Falta la cadena de conexión ConnectionStrings:Linde. Revisa appsettings.json y dotnet user-secrets.");

            services.AddDbContext<LindeDbContext>(o => o.UseNpgsql(conexion, npgsql => npgsql.UseNetTopologySuite()));
            services.AddScoped(typeof(IRepositorio<>), typeof(RepositorioEf<>));
            services.AddScoped<IRepositorioParametros, RepositorioParametrosEf>();
        }

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

        // Factory Method: lectores de capas de referencia (CU-11). Para shapefile, se registra un lector más.
        services.AddSingleton<ILectorCapa, LectorCapaGeoJson>();
        services.AddSingleton<FabricaLectoresCapa>();

        // Decorator: cada servicio real queda envuelto por su versión auditada.
        services.AddScoped<ServicioProductores>();
        services.AddScoped<VistasPredio>();
        services.AddScoped<ServicioPredios>();
        services.AddScoped<IServicioPredios>(sp =>
            new ServicioPrediosAuditado(sp.GetRequiredService<ServicioPredios>(), sp.GetRequiredService<AuditorOperaciones>()));
        services.AddScoped<ServicioPoligonos>();
        services.AddScoped<IServicioPoligonos>(sp =>
            new ServicioPoligonosAuditado(sp.GetRequiredService<ServicioPoligonos>(), sp.GetRequiredService<AuditorOperaciones>()));
        // RF-16: los archivos de los documentos van a una carpeta del servidor (o a memoria en las pruebas).
        if (UsaMemoria(configuracion)) services.AddSingleton<IAlmacenArchivos, AlmacenArchivosMemoria>();
        else services.AddSingleton<IAlmacenArchivos, AlmacenArchivosLocal>();
        services.AddScoped<ServicioDocumentos>();
        services.AddScoped<IServicioDocumentos>(sp =>
            new ServicioDocumentosAuditado(sp.GetRequiredService<ServicioDocumentos>(),
                sp.GetRequiredService<IRepositorio<Dominio.Documento>>(), sp.GetRequiredService<AuditorOperaciones>()));

        services.AddScoped<ServicioInspecciones>();
        services.AddScoped<IServicioInspecciones>(sp =>
            new ServicioInspeccionesAuditado(sp.GetRequiredService<ServicioInspecciones>(),
                sp.GetRequiredService<IRepositorio<Dominio.InspeccionCampo>>(), sp.GetRequiredService<AuditorOperaciones>()));

        services.AddScoped<ServicioCapas>();
        services.AddScoped<IServicioCapas>(sp =>
            new ServicioCapasAuditado(sp.GetRequiredService<ServicioCapas>(),
                sp.GetRequiredService<IRepositorio<Dominio.CapaReferencia>>(), sp.GetRequiredService<AuditorOperaciones>()));

        // RF-07: cruce del predio con las capas vigentes; lo usan la titularidad y la estrategia de la ABT.
        services.AddScoped<IConsultaCapas, ConsultaCapas>();

        // Adapter + Proxy: el validador de cobertura recibe IProveedorCobertura, que es el proxy de caché
        // delante del adaptador de Earth Engine (o de la simulación mientras no haya credenciales).
        services.AddHttpClient<EarthEngineAdapter>(c => c.Timeout = TimeSpan.FromSeconds(90));
        services.AddHttpClient(nameof(TokenCuentaServicio));
        services.AddSingleton<ITokenEarthEngine, TokenCuentaServicio>();
        services.AddSingleton<CoberturaSimulada>();
        services.AddScoped<ServicioImagenesPredio>();
        services.AddScoped<ServicioZonasCapas>();
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

    public static bool UsaMemoria(IConfiguration configuracion) =>
        string.Equals(configuracion["Datos:Proveedor"], "Memoria", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// En desarrollo aplica las migraciones pendientes al arrancar, para que cada integrante tenga la base al día
    /// sin ejecutar dotnet ef. En producción se aplican a mano con dotnet ef database update.
    /// </summary>
    public static void AplicarMigraciones(this WebApplication app)
    {
        if (UsaMemoria(app.Configuration) || !app.Environment.IsDevelopment()) return;

        using var alcance = app.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<LindeDbContext>();
        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "No se pudo conectar a PostgreSQL. Revisa que el servicio esté en ejecución, que exista la base \"linde\" " +
                "y que la contraseña esté en dotnet user-secrets (ConnectionStrings:Linde). Detalle: " + ex.Message, ex);
        }
    }
}
