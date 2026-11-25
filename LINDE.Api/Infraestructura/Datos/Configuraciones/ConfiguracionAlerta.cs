using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

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
