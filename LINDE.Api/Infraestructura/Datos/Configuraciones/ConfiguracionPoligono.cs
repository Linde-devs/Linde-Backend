using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

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
        b.Property(p => p.PrecisionesM).ComoJson();
        b.HasIndex(p => new { p.PredioId, p.Vigente });
        b.HasIndex(p => new { p.PredioId, p.Version }).IsUnique();

        // Cada versión queda en el historial (Prototype, RNF-05). Si se borra el predio, se borran sus versiones.
        b.HasOne<Predio>().WithMany().HasForeignKey(p => p.PredioId).OnDelete(DeleteBehavior.Cascade);
    }
}
