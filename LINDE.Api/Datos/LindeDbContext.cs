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

public class ConfiguracionUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        b.Property(u => u.Apellido).HasMaxLength(100).IsRequired();
        b.Property(u => u.Ci).HasMaxLength(15).IsRequired();
        b.Property(u => u.Correo).HasMaxLength(150).IsRequired();
        b.HasIndex(u => u.Correo).IsUnique();
        b.Property(u => u.ContrasenaHash).IsRequired();
        b.Property(u => u.Rol).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(u => u.ProductorId);
    }
}

public class ConfiguracionProductor : IEntityTypeConfiguration<Productor>
{
    public void Configure(EntityTypeBuilder<Productor> b)
    {
        b.ToTable("Productores");
        // El código se asigna justo después de insertar (necesita el id), por eso el índice ignora el valor vacío.
        b.Property(p => p.Codigo).HasMaxLength(12);
        b.HasIndex(p => p.Codigo).IsUnique().HasFilter("\"Codigo\" <> ''");
        b.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        b.Property(p => p.Apellido).HasMaxLength(100).IsRequired();
        b.Property(p => p.Ci).HasMaxLength(15).IsRequired();
        b.HasIndex(p => p.Ci).IsUnique();
        b.Property(p => p.Runpa).HasMaxLength(20);
        b.Property(p => p.Telefono).HasMaxLength(15).IsRequired();
        b.Property(p => p.Municipio).HasMaxLength(100).IsRequired();

        // Cada productor tiene una cuenta de acceso; la cuenta no se borra mientras exista el productor.
        b.HasOne<Usuario>().WithMany().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.UsuarioId).IsUnique();
    }
}

public class ConfiguracionPredio : IEntityTypeConfiguration<Predio>
{
    public void Configure(EntityTypeBuilder<Predio> b)
    {
        b.ToTable("Predios", t =>
        {
            t.HasCheckConstraint("CK_Predios_Superficie", "\"SuperficieDeclaradaHa\" > 0");
            t.HasCheckConstraint("CK_Predios_Desmonte", "\"DesmonteDeclaradoHa\" IS NULL OR \"DesmonteDeclaradoHa\" >= 0");
        });
        b.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        b.Property(p => p.Departamento).HasMaxLength(30).IsRequired().HasDefaultValue("Santa Cruz");
        b.Property(p => p.Municipio).HasMaxLength(100).IsRequired();
        b.Property(p => p.Cultivo).HasMaxLength(60).IsRequired();
        b.Property(p => p.NumeroTituloInra).HasMaxLength(30);
        b.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);

        // RF-02: un predio no se guarda sin productor. No se puede borrar un productor que tiene predios.
        b.HasOne<Productor>().WithMany().HasForeignKey(p => p.ProductorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.ProductorId);
    }
}

public class ConfiguracionPoligono : IEntityTypeConfiguration<Poligono>
{
    public void Configure(EntityTypeBuilder<Poligono> b)
    {
        b.ToTable("Poligonos");

        // Los vértices no son una columna: viven en Geometria, que es un geometry de PostGIS en WGS84.
        b.Ignore(p => p.Vertices);
        b.Property(p => p.Geometria)
            .HasColumnType("geometry(Geometry, 4326)")
            .UsePropertyAccessMode(PropertyAccessMode.Property)
            .IsRequired();

        // Índice espacial: ST_Intersects descarta en el índice los polígonos lejanos (RF-06, RNF-02).
        b.HasIndex(p => p.Geometria).HasMethod("gist");

        b.Property(p => p.Autor).HasMaxLength(150);
        b.Property(p => p.Motivo).HasMaxLength(300);
        b.HasIndex(p => new { p.PredioId, p.Vigente });
        b.HasIndex(p => new { p.PredioId, p.Version }).IsUnique();

        // Cada versión queda en el historial (Prototype, RNF-05). Si se borra el predio, se borran sus versiones.
        b.HasOne<Predio>().WithMany().HasForeignKey(p => p.PredioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConfiguracionDocumento : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> b)
    {
        b.ToTable("Documentos");
        b.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(40);
        b.Property(d => d.Numero).HasMaxLength(60);
        b.Property(d => d.TitularCi).HasMaxLength(15);
        b.HasOne<Predio>().WithMany().HasForeignKey(d => d.PredioId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(d => d.PredioId);
    }
}

public class ConfiguracionAlerta : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> b)
    {
        b.ToTable("Alertas");
        b.Property(a => a.Capa).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Prioridad).HasConversion<string>().HasMaxLength(10);
        b.Property(a => a.Mensaje).HasMaxLength(1000);
        b.HasOne<Predio>().WithMany().HasForeignKey(a => a.PredioId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(a => new { a.PredioId, a.Resuelta });
    }
}

public class ConfiguracionResultadoValidacion : IEntityTypeConfiguration<ResultadoValidacion>
{
    public void Configure(EntityTypeBuilder<ResultadoValidacion> b)
    {
        b.ToTable("ResultadosValidacion");
        // El resultado de cada capa se guarda junto, como JSON: siempre se lee completo.
        b.Property(r => r.Capas).ComoJson();
        b.Property(r => r.EstadoResultante).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Mensaje).HasMaxLength(1000);
        b.HasOne<Predio>().WithMany().HasForeignKey(r => r.PredioId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(r => r.PredioId);
    }
}

public class ConfiguracionExpediente : IEntityTypeConfiguration<Expediente>
{
    public void Configure(EntityTypeBuilder<Expediente> b)
    {
        b.ToTable("Expedientes");
        b.Property(e => e.Codigo).HasMaxLength(30).IsRequired();
        b.HasIndex(e => e.Codigo).IsUnique();
        b.Property(e => e.Productor).HasMaxLength(200);
        b.Property(e => e.CiProductor).HasMaxLength(15);
        b.Property(e => e.Predio).HasMaxLength(150);
        b.Property(e => e.Departamento).HasMaxLength(30);
        b.Property(e => e.Municipio).HasMaxLength(100);

        // El expediente es una fotografía del predio al aprobarse: sus listas se guardan tal como se generaron.
        b.Property(e => e.Vertices).ComoJson();
        b.Property(e => e.Validaciones).ComoJson();
        b.Property(e => e.Documentos).ComoJson();
        b.Property(e => e.Alertas).ComoJson();
        b.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);

        b.HasOne<Predio>().WithMany().HasForeignKey(e => e.PredioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConfiguracionBitacora : IEntityTypeConfiguration<RegistroBitacora>
{
    public void Configure(EntityTypeBuilder<RegistroBitacora> b)
    {
        // RNF-05: la bitácora no tiene claves foráneas, para que sobreviva aunque se borren los datos que registra.
        b.ToTable("Bitacora");
        b.Property(r => r.Usuario).HasMaxLength(150);
        b.Property(r => r.Operacion).HasMaxLength(100);
        b.Property(r => r.Entidad).HasMaxLength(50);
        b.Property(r => r.EntidadId).HasMaxLength(50);
        b.HasIndex(r => new { r.Entidad, r.EntidadId });
        b.HasIndex(r => r.Fecha);
    }
}

public class ConfiguracionParametros : IEntityTypeConfiguration<FilaParametros>
{
    public void Configure(EntityTypeBuilder<FilaParametros> b)
    {
        b.ToTable("Parametros");
        b.Property(f => f.Id).ValueGeneratedNever();
        b.Property(f => f.Datos).HasColumnType("jsonb");
    }
}

/// <summary>CU-11. Capas de referencia del INRA y la ABT.</summary>
public class ConfiguracionCapaReferencia : IEntityTypeConfiguration<CapaReferencia>
{
    public void Configure(EntityTypeBuilder<CapaReferencia> b)
    {
        b.ToTable("CapasReferencia");
        b.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
        b.Property(c => c.Fuente).HasMaxLength(300).IsRequired();
        b.Property(c => c.Archivo).HasMaxLength(260);
        b.Property(c => c.CargadaPor).HasMaxLength(150);
        b.HasIndex(c => new { c.Tipo, c.Vigente });
    }
}

/// <summary>Polígonos de las capas, con índice espacial para el cruce de RF-07 (ST_Intersects).</summary>
public class ConfiguracionElementoCapa : IEntityTypeConfiguration<ElementoCapa>
{
    public void Configure(EntityTypeBuilder<ElementoCapa> b)
    {
        b.ToTable("ElementosCapa");
        b.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(e => e.Codigo).HasMaxLength(60).IsRequired();
        b.Property(e => e.TitularCi).HasMaxLength(10);
        b.Property(e => e.Geometria).HasColumnType("geometry(Geometry, 4326)").IsRequired();
        b.HasIndex(e => e.Geometria).HasMethod("gist");
        b.HasIndex(e => e.CapaId);
        b.HasIndex(e => e.Tipo);
        b.HasOne<CapaReferencia>().WithMany().HasForeignKey(e => e.CapaId).OnDelete(DeleteBehavior.Cascade);
    }
}
