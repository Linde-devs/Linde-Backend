using LINDE.Api.Dominio;
using LINDE.Api.Expedientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

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
        b.Property(e => e.Inspecciones).ComoJson();
        b.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);

        b.HasOne<Predio>().WithMany().HasForeignKey(e => e.PredioId).OnDelete(DeleteBehavior.Cascade);
    }
}
