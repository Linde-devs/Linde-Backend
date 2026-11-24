using LINDE.Api.Dominio;

namespace LINDE.Tests;

public class DivisionPoliticaTests
{
    [Theory]
    [InlineData("Beni", "San Javier")]
    [InlineData("Santa Cruz", "San Javier")]
    [InlineData("Tarija", "Entre Ríos")]
    [InlineData("Cochabamba", "Entre Ríos")]
    public void MunicipioConElMismoNombreEnDosDepartamentos_SeAceptaEnAmbos(string departamento, string municipio)
    {
        Assert.Equal(municipio, DivisionPolitica.Municipio(departamento, municipio));
    }

    [Fact]
    public void MunicipioDeOtroDepartamento_SeRechaza()
    {
        Assert.Null(DivisionPolitica.Municipio("La Paz", "San Javier"));
    }

    [Fact]
    public void Departamento_SeReconoceSinTildesNiMayusculas()
    {
        Assert.Equal("Potosí", DivisionPolitica.Departamento("potosi"));
    }
}
