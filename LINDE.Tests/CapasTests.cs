using System.Text;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;
using LINDE.Api.Validacion;
using LINDE.Api.Validacion.UsoSuelo;

namespace LINDE.Tests;

/// <summary>CU-11 (capas de referencia) y RF-07 (cruce del predio con las capas del INRA y la ABT).</summary>
public class CapasTests
{
    private static readonly string Carpeta = Path.Combine(AppContext.BaseDirectory, "Archivos", "Capas");

    private static (ServicioCapas Servicio, IRepositorio<CapaReferencia> Capas, IRepositorio<ElementoCapa> Elementos) Armar()
    {
        var capas = new RepositorioMemoria<CapaReferencia>();
        var elementos = new RepositorioMemoria<ElementoCapa>();
        var servicio = new ServicioCapas(capas, elementos, new FabricaLectoresCapa([new LectorCapaGeoJson()]), new UsuarioFijo("admin@linde.bo"));
        return (servicio, capas, elementos);
    }

    private static CapaReferencia CargarArchivo(ServicioCapas servicio, TipoCapa tipo, string archivo)
    {
        using var contenido = File.OpenRead(Path.Combine(Carpeta, archivo));
        return servicio.Cargar(tipo, "Capa de prueba", "Datos ficticios", archivo, contenido);
    }

    private static Stream Texto(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Lector_LeeLosTitulosConSusAtributos()
    {
        using var contenido = File.OpenRead(Path.Combine(Carpeta, "titulos_inra_prueba.geojson"));
        var leidos = new LectorCapaGeoJson().Leer(contenido);

        Assert.Equal(4, leidos.Count);
        Assert.Equal("SPP-NAL-070167", leidos[0].Codigo);
        Assert.Equal("7845123", leidos[0].TitularCi);
        Assert.Equal(120, leidos[0].SuperficieHa);
    }

    [Theory]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[]}", "FeatureCollection")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[]}", "ningún polígono")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{},\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[[-63,-17.5],[-62.99,-17.5],[-62.99,-17.49],[-63,-17.5]]]}}]}", "número de título")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{\"codigo\":\"X\"},\"geometry\":{\"type\":\"Point\",\"coordinates\":[-63,-17.5]}}]}", "solo admite polígonos")]
    public void Lector_RechazaArchivosQueNoSonUnaCapaValida(string json, string mensaje)
    {
        var error = Assert.Throws<DatosInvalidosException>(() => new LectorCapaGeoJson().Leer(Texto(json)));
        Assert.Contains(mensaje, error.Message);
    }

    [Fact]
    public void Fabrica_RechazaFormatosSinLector()
    {
        var fabrica = new FabricaLectoresCapa([new LectorCapaGeoJson()]);
        Assert.Throws<DatosInvalidosException>(() => fabrica.CrearLector("capa.shp"));
        Assert.IsType<LectorCapaGeoJson>(fabrica.CrearLector("capa.geojson"));
    }

    [Fact]
    public void Servicio_UnaCapaNuevaReemplazaALaVigenteDelMismoTipo()
    {
        var (servicio, capas, elementos) = Armar();
        var primera = CargarArchivo(servicio, TipoCapa.TitulosInra, "titulos_inra_prueba.geojson");
        CargarArchivo(servicio, TipoCapa.AutorizacionesAbt, "autorizaciones_abt_prueba.geojson");
        var segunda = CargarArchivo(servicio, TipoCapa.TitulosInra, "titulos_inra_prueba.geojson");

        Assert.False(capas.Obtener(primera.Id)!.Vigente);
        Assert.True(capas.Obtener(segunda.Id)!.Vigente);
        Assert.Empty(elementos.Listar(e => e.CapaId == primera.Id));
        Assert.Equal(4, elementos.Listar(e => e.Tipo == TipoCapa.TitulosInra).Count);
        Assert.Equal(2, elementos.Listar(e => e.Tipo == TipoCapa.AutorizacionesAbt).Count);
    }

    [Fact]
    public void Servicio_UnArchivoConErroresNoCambiaLaCapaVigente()
    {
        var (servicio, capas, _) = Armar();
        var vigente = CargarArchivo(servicio, TipoCapa.TitulosInra, "titulos_inra_prueba.geojson");

        Assert.Throws<DatosInvalidosException>(() =>
            servicio.Cargar(TipoCapa.TitulosInra, "Mala", "Prueba", "mala.geojson", Texto("{\"type\":\"FeatureCollection\",\"features\":[]}")));

        Assert.True(capas.Obtener(vigente.Id)!.Vigente);
    }

    // ---------- RF-07: titularidad con la capa del INRA ----------

    private static async Task<ResultadoCapa> Titularidad(IConsultaCapas? consulta, List<Coordenada> vertices, string numeroTitulo)
    {
        var ctx = Ayudas.Contexto(Ayudas.Predio(), vertices,
            new Documento { Tipo = TipoDocumento.TituloPropiedad, Numero = numeroTitulo, TitularCi = "7845123", SuperficieHa = 120 });
        ctx.AreaPoligonoHa = LINDE.Api.Geometria.Geometrias.AreaHectareas(vertices);
        await ValidadorCapa.ArmarCadena([new ValidadorTitularidad(consulta)]).ProcesarAsync(ctx);
        return ctx.Resultados[0];
    }

    private static IConsultaCapas ConCapas(params (TipoCapa Tipo, string Archivo)[] archivos)
    {
        var (servicio, capas, elementos) = Armar();
        foreach (var (tipo, archivo) in archivos) CargarArchivo(servicio, tipo, archivo);
        return new ConsultaCapas(capas, elementos);
    }

    [Fact]
    public async Task Titularidad_ElPoligonoCoincideConSuTituloEnLaCapa()
    {
        var resultado = await Titularidad(ConCapas((TipoCapa.TitulosInra, "titulos_inra_prueba.geojson")), Ayudas.Cuadrado(), "SPP-NAL-070167");

        Assert.Equal(EstadoCapa.Conforme, resultado.Estado);
        Assert.Contains("Coincide con el título SPP-NAL-070167", resultado.Mensaje);
    }

    [Fact]
    public async Task Titularidad_ElNumeroDeTituloNoEsElDeLaCapa()
    {
        // Mismo número escrito distinto se acepta; un número inventado no.
        var consulta = ConCapas((TipoCapa.TitulosInra, "titulos_inra_prueba.geojson"));
        Assert.Equal(EstadoCapa.Conforme, (await Titularidad(consulta, Ayudas.Cuadrado(), "spp nal 070167")).Estado);

        var resultado = await Titularidad(consulta, Ayudas.Cuadrado(), "SPP-NAL-999999");
        Assert.Equal(EstadoCapa.Alerta, resultado.Estado);
        Assert.Contains("el título registrado es SPP-NAL-999999", resultado.Mensaje);
    }

    [Fact]
    public async Task Titularidad_ElTitularDeLaCapaNoEsElDeclaranteYNoSeMuestraSuCi()
    {
        // Este cuadrado cae sobre el título SPP-NAL-071002, cuyo titular en la capa tiene otro CI.
        var resultado = await Titularidad(ConCapas((TipoCapa.TitulosInra, "titulos_inra_prueba.geojson")),
            Ayudas.Cuadrado(lon: -63.0105, lat: -17.4995, lado: 0.008), "SPP-NAL-071002");

        Assert.Equal(EstadoCapa.Alerta, resultado.Estado);
        Assert.Contains("no es el declarante", resultado.Mensaje);
        Assert.DoesNotContain("4567891", resultado.Mensaje);
    }

    [Fact]
    public async Task Titularidad_ParteDelPoligonoQuedaFueraDelTitulo()
    {
        // Corrido 0.004° al este: una parte sale del título SPP-NAL-070167.
        var resultado = await Titularidad(ConCapas((TipoCapa.TitulosInra, "titulos_inra_prueba.geojson")),
            Ayudas.Cuadrado(lon: -62.996), "SPP-NAL-070167");

        Assert.Equal(EstadoCapa.Alerta, resultado.Estado);
        Assert.Contains("quedan fuera del título", resultado.Mensaje);
    }

    [Fact]
    public async Task Titularidad_SinTituloEnLaCapaGeneraAlerta()
    {
        var resultado = await Titularidad(ConCapas((TipoCapa.TitulosInra, "titulos_inra_prueba.geojson")),
            Ayudas.Cuadrado(lon: -62.90, lat: -17.40), "SPP-NAL-070167");

        Assert.Equal(EstadoCapa.Alerta, resultado.Estado);
        Assert.Contains("no coincide con ningún predio titulado", resultado.Mensaje);
    }

    [Fact]
    public async Task Titularidad_SinCapaCargadaUsaLosDatosDeclarados()
    {
        var resultado = await Titularidad(ConCapas(), Ayudas.Cuadrado(), "SPP-NAL-999999");

        Assert.Equal(EstadoCapa.Conforme, resultado.Estado);
        Assert.Contains("No hay capa del INRA cargada", resultado.Mensaje);
    }

    // ---------- RF-07: uso de suelo con la capa de la ABT ----------

    private static EvaluacionUsoSuelo UsoSuelo(IConsultaCapas consulta, List<Coordenada> vertices, string numeroPop)
    {
        var ctx = Ayudas.Contexto(Ayudas.Predio(desmonte: 0), vertices,
            new Documento { Tipo = TipoDocumento.PlanOrdenamientoPredial, Numero = numeroPop, SuperficieHa = 200 });
        ctx.AreaPoligonoHa = LINDE.Api.Geometria.Geometrias.AreaHectareas(vertices);
        return new EstrategiaAutorizacionAbt(consulta).Evaluar(ctx);
    }

    [Fact]
    public void Abt_ElPoligonoCubiertoPorSuAutorizacionEstaAutorizado()
    {
        var evaluacion = UsoSuelo(ConCapas((TipoCapa.AutorizacionesAbt, "autorizaciones_abt_prueba.geojson")),
            Ayudas.Cuadrado(), "ABT-POP-2024-0009");

        Assert.Equal(EstadoUsoSuelo.Autorizado, evaluacion.Estado);
        Assert.Contains("ABT-POP-2024-0009", evaluacion.Mensaje);
    }

    [Fact]
    public void Abt_ParteSinAutorizacionNoEstaAutorizada()
    {
        var evaluacion = UsoSuelo(ConCapas((TipoCapa.AutorizacionesAbt, "autorizaciones_abt_prueba.geojson")),
            Ayudas.Cuadrado(lon: -62.996), "ABT-POP-2024-0009");

        Assert.Equal(EstadoUsoSuelo.NoAutorizado, evaluacion.Estado);
        Assert.Contains("no están cubiertas", evaluacion.Mensaje);
    }

    [Fact]
    public void Abt_ElPopRegistradoNoEsElDeLaCapa()
    {
        var evaluacion = UsoSuelo(ConCapas((TipoCapa.AutorizacionesAbt, "autorizaciones_abt_prueba.geojson")),
            Ayudas.Cuadrado(), "ABT-POP-2020-0001");

        Assert.Equal(EstadoUsoSuelo.NoAutorizado, evaluacion.Estado);
        Assert.Contains("el POP registrado es ABT-POP-2020-0001", evaluacion.Mensaje);
    }
}
