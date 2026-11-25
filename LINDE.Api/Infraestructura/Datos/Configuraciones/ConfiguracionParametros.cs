using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LINDE.Api.Datos;

public class ConfiguracionParametros : IEntityTypeConfiguration<FilaParametros>
{
    public void Configure(EntityTypeBuilder<FilaParametros> b)
    {
        b.ToTable("Parametros");
        b.Property(f => f.Id).ValueGeneratedNever();
        b.Property(f => f.Datos).HasColumnType("jsonb");
    }
}
