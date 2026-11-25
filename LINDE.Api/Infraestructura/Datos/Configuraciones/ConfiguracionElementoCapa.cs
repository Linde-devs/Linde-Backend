using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

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
