using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Configuracion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;
using LINDE.Api.Validacion;
using LINDE.Api.Validacion.UsoSuelo;
using Microsoft.Extensions.DependencyInjection;

namespace LINDE.Tests;

/// <summary>RF-07: las hectáreas del predio fuera del título o de la autorización, para resaltarlas en el mapa.</summary>
public class ZonasCapasTests
{
    private static readonly string Carpeta = Path.Combine(AppContext.BaseDirectory, "Archivos", "Capas");

    private static List<Coordenada> Rectangulo(double oeste, double sur, double este, double norte) =>
    [
        new(oeste, sur), new(este, sur), new(este, norte), new(oeste, norte), new(oeste, sur)
    ];

    private static (ServicioZonasCapas Servicio, IRepositorio<Predio> Predios, IRepositorio<Poligono> Poligonos) Armar(bool conCapas = true)
    {
        var s = new ServiceCollection();
        s.AddSingleton(typeof(IRepositorio<>), typeof(RepositorioMemoria<>));
        s.AddSingleton<IRepositorioParametros, RepositorioParametrosMemoria>();
        s.AddSingleton<IServicioParametros, ServicioParametros>();
        s.AddSingleton<IUsuarioActual>(new UsuarioFijo("admin@linde.bo"));
        s.AddSingleton<ControlAcceso>();
        s.AddSingleton<IConsultaCapas, ConsultaCapas>();
        s.AddSingleton<EstrategiaAutorizacionAbt>();
        s.AddSingleton<EstrategiaExencionLey741>();
        s.AddSingleton<SelectorEstrategiaUsoSuelo>();
        s.AddSingleton<ServicioZonasCapas>();
        var p = s.BuildServiceProvider();

        if (conCapas)
        {
            var capas = new ServicioCapas(p.GetRequiredService<IRepositorio<CapaReferencia>>(), p.GetRequiredService<IRepositorio<ElementoCapa>>(),
                new FabricaLectoresCapa([new LectorCapaGeoJson()]), new UsuarioFijo("admin@linde.bo"));
            foreach (var (tipo, archivo) in new[] { (TipoCapa.TitulosInra, "titulos_inra_prueba.geojson"), (TipoCapa.AutorizacionesAbt, "autorizaciones_abt_prueba.geojson") })
            {
                using var contenido = File.OpenRead(Path.Combine(Carpeta, archivo));
                capas.Cargar(tipo, "Capa de prueba", "Datos ficticios", archivo, contenido);
            }
        }
        return (p.GetRequiredService<ServicioZonasCapas>(), p.GetRequiredService<IRepositorio<Predio>>(), p.GetRequiredService<IRepositorio<Poligono>>());
    }

    private static int Registrar(IRepositorio<Predio> predios, IRepositorio<Poligono> poligonos, List<Coordenada> v, double? desmonte = null)
    {
        var predio = Ayudas.Predio(id: 0);
        predio.DesmonteDeclaradoHa = desmonte;
        predio = predios.Agregar(predio);
        poligonos.Agregar(new Poligono { PredioId = predio.Id, Vertices = v });
        return predio.Id;
    }

    [Fact]
    public void Zonas_MarcaLaParteFueraDelTituloYDeLasAutorizaciones()
    {
        var (servicio, predios, poligonos) = Armar();
        // El título SPP-NAL-070167 y la autorización ABT-POP-2024-0009 llegan hasta la longitud -62.99; el predio sigue hasta -62.985.
        var id = Registrar(predios, poligonos, Rectangulo(-63.0005, -17.5005, -62.985, -17.4905));

        var zonas = servicio.Calcular(id);

        var inra = zonas.Single(z => z.Capa == "TitulosInra");
        var abt = zonas.Single(z => z.Capa == "AutorizacionesAbt");
        Assert.Equal("Fuera del título SPP-NAL-070167 del INRA", inra.Titulo);
        Assert.True(inra.SuperaTolerancia);
        // La franja de 0,005 grados es un tercio del ancho del predio.
        Assert.InRange(inra.Porcentaje, 31, 34);
        Assert.InRange(abt.AreaHa, inra.AreaHa - 0.5, inra.AreaHa + 0.5);
    }

    [Fact]
    public void Zonas_UnPredioExentoPorLaLey741NoMuestraZonaDeLaAbt()
    {
        var (servicio, predios, poligonos) = Armar();
        // Unas 47 ha, una parte fuera del título SPP-NAL-070168: exento de autorización por tener menos de 50 ha.
        var id = Registrar(predios, poligonos, Rectangulo(-62.988, -17.4935, -62.980, -17.4885));

        var zonas = servicio.Calcular(id);

        Assert.Single(zonas);
        Assert.Equal("TitulosInra", zonas[0].Capa);
    }

    [Fact]
    public void Zonas_SinCapasCargadasNoHayNadaQueResaltar()
    {
        var (servicio, predios, poligonos) = Armar(conCapas: false);
        var id = Registrar(predios, poligonos, Rectangulo(-63.0005, -17.5005, -62.985, -17.4905));
        Assert.Empty(servicio.Calcular(id));
    }
}
