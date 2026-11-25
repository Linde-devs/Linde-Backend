using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

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
