using LINDE.Api.Autenticacion;
using LINDE.Api.Dominio;

namespace LINDE.Api.Infraestructura;

/// <summary>Convierte las excepciones del dominio en respuestas HTTP con un mensaje claro en español.</summary>
public class ManejoErrores(RequestDelegate siguiente, ILogger<ManejoErrores> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (Exception ex)
        {
            var codigo = ex switch
            {
                CredencialesInvalidasException => StatusCodes.Status401Unauthorized,
                RecursoNoEncontradoException => StatusCodes.Status404NotFound,
                OperacionNoPermitidaException => StatusCodes.Status409Conflict,
                ArgumentException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            if (codigo == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Error no controlado");

            contexto.Response.StatusCode = codigo;
            await contexto.Response.WriteAsJsonAsync(new
            {
                error = codigo == StatusCodes.Status500InternalServerError ? "Ocurrió un error inesperado." : ex.Message,
                campo = (ex as DatosInvalidosException)?.Campo
            });
        }
    }
}
