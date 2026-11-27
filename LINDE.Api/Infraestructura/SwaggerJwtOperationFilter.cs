using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LINDE.Api.Infraestructura;

/// <summary>
/// Documenta en Swagger qué endpoints piden el token. En LINDE todos lo piden por defecto (política de autorización
/// por defecto en ConfiguracionAutenticacion), salvo los marcados con [AllowAnonymous]: login, registro y consulta
/// pública. Por eso el candado se pone en todo lo que no tenga [AllowAnonymous].
/// </summary>
public sealed class SwaggerJwtOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return;

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(ConfiguracionSwagger.Esquema, context.Document)] = []
            }
        ];
    }
}
