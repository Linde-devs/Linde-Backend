using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

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
