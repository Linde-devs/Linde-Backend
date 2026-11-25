using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

public class ConfiguracionDocumento : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> b)
    {
        b.ToTable("Documentos");
        b.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(40);
        b.Property(d => d.Numero).HasMaxLength(60);
        b.Property(d => d.TitularCi).HasMaxLength(15);
        b.Property(d => d.ArchivoClave).HasMaxLength(80);
        b.Property(d => d.ArchivoNombre).HasMaxLength(260);
        b.Property(d => d.ArchivoTipo).HasMaxLength(60);
        b.Ignore(d => d.TieneArchivo);
        b.HasOne<Predio>().WithMany().HasForeignKey(d => d.PredioId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(d => d.PredioId);
    }
}
