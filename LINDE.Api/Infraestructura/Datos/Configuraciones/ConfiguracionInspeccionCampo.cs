using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

public class ConfiguracionInspeccionCampo : IEntityTypeConfiguration<InspeccionCampo>
{
    public void Configure(EntityTypeBuilder<InspeccionCampo> b)
    {
        b.ToTable("InspeccionesCampo");
        b.Property(i => i.Tecnico).HasMaxLength(100).IsRequired();
        b.Property(i => i.Resultado).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.Comentario).HasMaxLength(1000);
        b.Property(i => i.RegistradaPor).HasMaxLength(150);
        b.HasOne<Predio>().WithMany().HasForeignKey(i => i.PredioId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(i => i.PredioId);
    }
}
