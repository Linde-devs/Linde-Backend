using System.Text.Json;
using System.Text.Json.Serialization;
using LINDE.Api.Dominio;
using LINDE.Api.Expedientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

/// <summary>Opciones de JSON para las columnas jsonb: enumeraciones como texto, igual que en la API.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>
/// Contexto de EF Core para PostgreSQL + PostGIS. Los mapeos están en las clases de configuración de abajo
/// (Fluent API), así las entidades del dominio no dependen de EF Core.
/// </summary>
public class LindeDbContext(DbContextOptions<LindeDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Productor> Productores => Set<Productor>();
    public DbSet<Predio> Predios => Set<Predio>();
    public DbSet<Poligono> Poligonos => Set<Poligono>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<InspeccionCampo> InspeccionesCampo => Set<InspeccionCampo>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<ResultadoValidacion> ResultadosValidacion => Set<ResultadoValidacion>();
    public DbSet<Expediente> Expedientes => Set<Expediente>();
    public DbSet<RegistroBitacora> Bitacora => Set<RegistroBitacora>();
    public DbSet<FilaParametros> Parametros => Set<FilaParametros>();
    public DbSet<CapaReferencia> CapasReferencia => Set<CapaReferencia>();
    public DbSet<ElementoCapa> ElementosCapa => Set<ElementoCapa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // La migración crea la extensión si todavía no existe en la base.
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LindeDbContext).Assembly);
    }
}

/// <summary>Guarda una lista u objeto como jsonb y compara por contenido para detectar cambios.</summary>
internal static class ColumnaJson
{
    public static PropertyBuilder<TValor> ComoJson<TValor>(this PropertyBuilder<TValor> propiedad) where TValor : class, new()
    {
        var comparador = new ValueComparer<TValor>(
            (a, b) => JsonSerializer.Serialize(a, Json.Opciones) == JsonSerializer.Serialize(b, Json.Opciones),
            v => JsonSerializer.Serialize(v, Json.Opciones).GetHashCode(),
            v => JsonSerializer.Deserialize<TValor>(JsonSerializer.Serialize(v, Json.Opciones), Json.Opciones)!);

        propiedad
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json.Opciones),
                v => JsonSerializer.Deserialize<TValor>(v, Json.Opciones) ?? new TValor())
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(comparador);
        return propiedad;
    }
}
