using LINDE.Api.Autenticacion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;

namespace LINDE.Tests;

/// <summary>RF-04: cada vértice capturado con el GPS guarda su precisión en metros.</summary>
public class PrecisionGpsTests
{
    private static (ServicioPoligonos Servicio, int PredioId) Crear()
    {
        var predios = new RepositorioMemoria<Predio>();
        var usuario = new UsuarioFijo();
        var servicio = new ServicioPoligonos(predios, new RepositorioMemoria<Poligono>(),
            new FabricaLectores([new LectorGeoJson(), new LectorKml()]), usuario, new ControlAcceso(usuario));
        return (servicio, predios.Agregar(Ayudas.Predio(id: 0)).Id);
    }

    [Fact]
    public void Gps_SeGuardaLaPrecisionDeCadaVerticeYNullEnLosMarcadosAMano()
    {
        var (servicio, predioId) = Crear();
        var v = Ayudas.Cuadrado();
        var precisiones = new double?[] { 4.26, 7, null, 3.5, 4.26 };

        var p = servicio.Registrar(predioId, v, "Recorrido con GPS", precisiones);

        Assert.Equal(5, p.PrecisionesM.Count);
        Assert.Equal(4.3, p.PrecisionesM[0]!.Value, 3);
        Assert.Null(p.PrecisionesM[2]);
    }

    [Fact]
    public void Gps_SinPrecisionesLaListaQuedaVacia()
    {
        var (servicio, predioId) = Crear();
        var p = servicio.Registrar(predioId, Ayudas.Cuadrado(), "Dibujo con clics", new double?[] { null, null, null, null, null });
        Assert.Empty(p.PrecisionesM);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void Gps_SeRechazanPrecisionesQueNoCorrespondenONoSonValidas(int caso)
    {
        var (servicio, predioId) = Crear();
        var precisiones = caso == 3
            ? new double?[] { 4, 5, 6 }
            : new double?[] { 4, -2, 6, 7, 4 };

        Assert.Throws<DatosInvalidosException>(() => servicio.Registrar(predioId, Ayudas.Cuadrado(), null, precisiones));
    }

    [Fact]
    public void Gps_AlCorregirUnVerticeAManoPierdeSuPrecisionYLaVersionAnteriorLaConserva()
    {
        var (servicio, predioId) = Crear();
        var v = Ayudas.Cuadrado();
        servicio.Registrar(predioId, v, "Recorrido con GPS", new double?[] { 4, 5, 6, 7, 4 });

        var nueva = servicio.EditarVertice(predioId, 1, new Coordenada(v[1].Lon + 0.0001, v[1].Lat), null);
        var anterior = servicio.Historial(predioId).Single(p => p.Version == 1);

        Assert.Null(nueva.PrecisionesM[1]);
        Assert.Equal(6, nueva.PrecisionesM[2]!.Value, 3);
        Assert.Equal(5, anterior.PrecisionesM[1]!.Value, 3);
    }
}
