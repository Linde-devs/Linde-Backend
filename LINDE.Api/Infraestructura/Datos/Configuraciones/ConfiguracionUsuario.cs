using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

public class ConfiguracionUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        b.Property(u => u.Apellido).HasMaxLength(100).IsRequired();
        b.Property(u => u.Ci).HasMaxLength(15).IsRequired();
        b.Property(u => u.Correo).HasMaxLength(150).IsRequired();
        b.HasIndex(u => u.Correo).IsUnique();
        b.Property(u => u.ContrasenaHash).IsRequired();
        b.Property(u => u.Rol).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(u => u.ProductorId);
    }
}
