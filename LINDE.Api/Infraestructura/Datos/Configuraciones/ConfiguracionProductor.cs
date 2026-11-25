using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

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
