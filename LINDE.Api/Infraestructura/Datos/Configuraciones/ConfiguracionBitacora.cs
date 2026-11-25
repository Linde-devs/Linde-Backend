using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

public class ConfiguracionBitacora : IEntityTypeConfiguration<RegistroBitacora>
{
    public void Configure(EntityTypeBuilder<RegistroBitacora> b)
    {
        // RNF-05: la bitácora no tiene claves foráneas, para que sobreviva aunque se borren los datos que registra.
        b.ToTable("Bitacora");
        b.Property(r => r.Usuario).HasMaxLength(150);
        b.Property(r => r.Operacion).HasMaxLength(100);
        b.Property(r => r.Entidad).HasMaxLength(50);
        b.Property(r => r.EntidadId).HasMaxLength(50);
        b.HasIndex(r => new { r.Entidad, r.EntidadId });
        b.HasIndex(r => r.Fecha);
    }
}
