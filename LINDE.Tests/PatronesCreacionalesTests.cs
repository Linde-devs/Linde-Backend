using System.Text;
using LINDE.Api.Configuracion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Expedientes;
using LINDE.Api.Importacion;
using Microsoft.Extensions.DependencyInjection;

namespace LINDE.Tests;

public class PatronesCreacionalesTests
{
    private static readonly FabricaLectores Fabrica = new(new ILectorPoligono[] { new LectorGeoJson(), new LectorKml() });

    // ---------- Factory Method: lectores de polígono ----------

    [Fact]
    public void FactoryMethod_EntregaElLectorSegunLaExtension()
    {
        Assert.IsType<LectorKml>(Fabrica.CrearLector("predio.KML"));
        Assert.IsType<LectorGeoJson>(Fabrica.CrearLector("predio.geojson"));
        Assert.IsType<LectorGeoJson>(Fabrica.CrearLector("predio.json"));
    }

    [Fact]
    public void FactoryMethod_RechazaFormatosNoSoportados()
    {
        var error = Assert.Throws<DatosInvalidosException>(() => Fabrica.CrearLector("predio.shp"));
        Assert.Contains(".shp", error.Message);
    }

    [Fact]
    public void LectorGeoJson_LeeLongitudPrimeroYLatitudDespues()
    {
        const string json = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{},
             "geometry":{"type":"Polygon","coordinates":[[[-63.0,-17.5],[-62.99,-17.5],[-62.99,-17.49],[-63.0,-17.5]]]}}]}
            """;
        var vertices = Fabrica.CrearLector("a.geojson").Leer(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(4, vertices.Count);
        Assert.Equal(new Coordenada(-63.0, -17.5), vertices[0]);
    }

    [Fact]
    public void LectorKml_LeeLasTuplasDeCoordenadas()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2"><Placemark><Polygon><outerBoundaryIs><LinearRing>
            <coordinates>-63.0,-17.5,0 -62.99,-17.5,0 -62.99,-17.49,0 -63.0,-17.5,0</coordinates>
            </LinearRing></outerBoundaryIs></Polygon></Placemark></kml>
            """;
        var vertices = Fabrica.CrearLector("a.kml").Leer(new MemoryStream(Encoding.UTF8.GetBytes(kml)));

        Assert.Equal(4, vertices.Count);
        Assert.Equal(new Coordenada(-62.99, -17.49), vertices[2]);
    }

    // ---------- Prototype: versiones del polígono ----------

    [Fact]
    public void Prototype_ClonarHaceUnaCopiaProfunda()
    {
        var original = new Poligono { PredioId = 1, Version = 3, Vertices = Ayudas.Cuadrado() };

        var copia = original.Clonar();
        copia.Vertices[0] = new Coordenada(0, 0);

        Assert.NotSame(original.Vertices, copia.Vertices);
        Assert.Equal(new Coordenada(-63.0, -17.5), original.Vertices[0]);
        Assert.Equal(3, copia.Version);
        Assert.Equal(original.PredioId, copia.PredioId);
    }

    // ---------- Builder: expediente ----------

    private static ExpedienteBuilder BuilderCompleto(Predio predio) => new ExpedienteBuilder()
        .ConProductor(new Productor { Nombre = "Juan", Apellido = "Pérez", Ci = "7845123" })
        .ConPredio(predio)
        .ConPoligono(new Poligono { PredioId = predio.Id, Vertices = Ayudas.Cuadrado() })
        .ConValidacion(new ResultadoValidacion { PredioId = predio.Id });

    [Fact]
    public void Builder_NoGeneraElExpedienteDeUnPredioNoAprobado()
    {
        var predio = Ayudas.Predio();
        Assert.Throws<OperacionNoPermitidaException>(() => BuilderCompleto(predio).Construir("LINDE-1"));
    }

    [Fact]
    public void Builder_IndicaLasSeccionesQueFaltan()
    {
        var error = Assert.Throws<OperacionNoPermitidaException>(() =>
            new ExpedienteBuilder().ConPredio(Ayudas.Predio()).Construir("LINDE-1"));

        Assert.Contains("productor", error.Message);
        Assert.Contains("polígono", error.Message);
    }

    [Fact]
    public void Builder_ConstruyeElExpedienteDeUnPredioAprobado()
    {
        var predio = Ayudas.Predio();
        predio.EnviarARevision(hayAlertas: false);

        var expediente = BuilderCompleto(predio).ConResolucionVerificador(null).Construir("LINDE-2026-000001");

        Assert.Equal("LINDE-2026-000001", expediente.Codigo);
        Assert.Equal("Juan Pérez", expediente.Productor);
        Assert.InRange(expediente.AreaPoligonoHa, 95, 105);
    }

    // ---------- Singleton por contenedor: parámetros ----------

    [Fact]
    public void Singleton_ElContenedorEntregaSiempreLaMismaInstanciaYSeRecarga()
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IRepositorioParametros, RepositorioParametrosMemoria>();
        servicios.AddSingleton<IServicioParametros, ServicioParametros>();
        using var proveedor = servicios.BuildServiceProvider();

        var primero = proveedor.GetRequiredService<IServicioParametros>();
        var segundo = proveedor.GetRequiredService<IServicioParametros>();
        Assert.Same(primero, segundo);

        proveedor.GetRequiredService<IRepositorioParametros>().Guardar(new Parametros { UmbralPoligonoObligatorioHa = 6 });
        Assert.Equal(4, primero.Actuales.UmbralPoligonoObligatorioHa);

        primero.Recargar();
        Assert.Equal(6, segundo.Actuales.UmbralPoligonoObligatorioHa);
    }
}
