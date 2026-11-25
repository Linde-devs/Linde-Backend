using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

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
