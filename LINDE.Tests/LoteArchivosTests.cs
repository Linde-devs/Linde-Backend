using LINDE.Api.Autenticacion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;

namespace LINDE.Tests;

/// <summary>
/// Criterio de aceptación del RF-03: de un lote de 20 archivos (10 válidos y 10 inválidos) se aceptan los 10 válidos
/// y se rechazan los 10 inválidos indicando el tipo de error. Los archivos están en LINDE.Tests/Archivos;
/// en los inválidos, el tipo de error esperado es la última parte del nombre (por ejemplo 03_Autointerseccion.geojson).
/// </summary>
public class LoteArchivosTests
{
    private static string Carpeta(string nombre) => Path.Combine(AppContext.BaseDirectory, "Archivos", nombre);

    private static (ServicioPoligonos Servicio, IRepositorio<Predio> Predios) Crear()
    {
        var predios = new RepositorioMemoria<Predio>();
        var usuario = new UsuarioFijo();
        var servicio = new ServicioPoligonos(predios, new RepositorioMemoria<Poligono>(),
            new FabricaLectores([new LectorGeoJson(), new LectorKml()]), usuario, new ControlAcceso(usuario));
        return (servicio, predios);
    }

    private static Poligono Importar(ServicioPoligonos servicio, IRepositorio<Predio> predios, string ruta)
    {
        var predio = predios.Agregar(Ayudas.Predio(id: 0));
        using var contenido = File.OpenRead(ruta);
        return servicio.Importar(predio.Id, Path.GetFileName(ruta), contenido, null);
    }

    [Fact]
    public void Lote_AceptaLos10ValidosYRechazaLos10InvalidosConSuTipoDeError()
    {
        var (servicio, predios) = Crear();
        var validos = Directory.GetFiles(Carpeta("Validos")).OrderBy(f => f).ToList();
        var invalidos = Directory.GetFiles(Carpeta("Invalidos")).OrderBy(f => f).ToList();
        Assert.Equal(10, validos.Count);
        Assert.Equal(10, invalidos.Count);

        foreach (var ruta in validos)
        {
            var poligono = Importar(servicio, predios, ruta);
            Assert.True(Geometrias.AreaHectareas(poligono.Vertices) > 0);
        }

        foreach (var ruta in invalidos)
        {
            var esperado = Enum.Parse<ErrorGeometria>(Path.GetFileNameWithoutExtension(ruta).Split('_')[^1]);
            var error = Assert.Throws<GeometriaInvalidaException>(() => Importar(servicio, predios, ruta));
            Assert.Equal(esperado, error.Tipo);
        }
    }

    [Theory]
    [InlineData(-63.18, -17.78, true)]   // Santa Cruz de la Sierra
    [InlineData(-68.15, -16.50, true)]   // La Paz
    [InlineData(-57.80, -18.96, true)]   // Puerto Suárez, junto a la frontera
    [InlineData(-75.00, -17.00, false)]  // Océano Pacífico
    [InlineData(-70.31, -18.48, false)]  // Arica, Chile (costa)
    [InlineData(-17.78, -63.18, false)]  // latitud y longitud invertidas
    [InlineData(63.18, 17.78, false)]    // signos olvidados
    public void Punto_SoloSeAceptaDentroDeBolivia(double lon, double lat, bool aceptado)
    {
        var problema = RevisionGeometrica.Revisar([new Coordenada(lon, lat)]);
        Assert.Equal(aceptado, problema is null);
        if (!aceptado) Assert.Equal(ErrorGeometria.FueraDeBolivia, problema!.Tipo);
    }

    [Fact]
    public void Poligono_ConUnVerticeEnElMarSeRechazaIndicandoCual()
    {
        var v = new List<Coordenada> { new(-63.0, -17.5), new(-75.0, -17.5), new(-63.0, -17.4), new(-63.0, -17.5) };
        var problema = RevisionGeometrica.Revisar(v);
        Assert.Equal(ErrorGeometria.FueraDeBolivia, problema!.Tipo);
        Assert.Contains("vértice 2", problema.Mensaje);
    }
}
