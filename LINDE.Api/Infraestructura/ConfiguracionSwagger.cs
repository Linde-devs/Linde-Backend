using System.Reflection;
using Microsoft.OpenApi;

namespace LINDE.Api.Infraestructura;

/// <summary>
/// Swagger (OpenAPI): documenta todos los endpoints y permite probarlos desde el navegador en /swagger.
/// Casi todos piden sesión: se inicia sesión con POST /api/auth/login, se copia el token y se pega en "Authorize".
/// </summary>
public static class ConfiguracionSwagger
{
    private const string Esquema = "Bearer";

    public static IServiceCollection AgregarSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "LINDE API",
                Version = "v1",
                Description = "Gestión y validación de polígonos de predios agrícolas para la certificación de exportación. " +
                              "Para probar los endpoints protegidos: POST /api/auth/login, copia el token de la respuesta " +
                              "y pégalo en el botón Authorize (sin escribir \"Bearer\")."
            });

            o.AddSecurityDefinition(Esquema, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Token JWT que devuelve POST /api/auth/login."
            });
            o.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(Esquema, documento)] = []
            });

            // Los comentarios /// del código aparecen como descripción de cada endpoint y de cada campo.
            var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xml))
                o.IncludeXmlComments(xml);
        });
        return services;
    }

    public static WebApplication UsarSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(o =>
        {
            o.SwaggerEndpoint("/swagger/v1/swagger.json", "LINDE API v1");
            o.DocumentTitle = "LINDE API";
        });
        return app;
    }
}
