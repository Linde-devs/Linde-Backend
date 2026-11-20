using System.Text.Json.Serialization;
using LINDE.Api.Autenticacion;
using LINDE.Api.Infraestructura;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AgregarLinde(builder.Configuration);
builder.Services.AgregarAutenticacionJwt(builder.Configuration);

builder.Services.AddCors(o => o.AddPolicy("Frontend", politica => politica
    .WithOrigins(builder.Configuration.GetSection("Frontend:Origenes").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Crea o actualiza las tablas en PostgreSQL (solo en desarrollo) y luego las cuentas de la sección
// CuentasIniciales: el administrador inicial.
app.AplicarMigraciones();
app.CrearCuentasIniciales();

app.UseMiddleware<ManejoErrores>();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new { sistema = "LINDE API", estado = "en línea" })).AllowAnonymous();

app.Run();
