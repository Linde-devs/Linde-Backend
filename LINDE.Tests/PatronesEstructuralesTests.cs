using System.Net;
using System.Text;
using LINDE.Api.Auditoria;
using LINDE.Api.Cobertura;
using LINDE.Api.Configuracion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Servicios;
using LINDE.Api.Validacion;
using LINDE.Api.Validacion.UsoSuelo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LINDE.Tests;

public class PatronesEstructuralesTests
{
    // ---------- Adapter: Earth Engine ----------

    private class RespuestaFija(string json) : HttpMessageHandler
    {
        public string? CuerpoEnviado { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CuerpoEnviado = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public async Task Adapter_TraduceElPoligonoYLaRespuestaDeEarthEngine()
    {
        var manejador = new RespuestaFija("""{"forestFractionReference":0.62,"forestFractionCurrent":0.48,"imageDate":"2026-10-30"}""");
        var opciones = Options.Create(new OpcionesCobertura { Proveedor = "EarthEngine", UrlServicio = "https://ee.ejemplo/analizar" });
        var adaptador = new EarthEngineAdapter(new HttpClient(manejador), opciones);

        var resultado = await adaptador.AnalizarAsync(Ayudas.Cuadrado(), new DateOnly(2020, 12, 31));

        Assert.Equal(62, resultado.BosquePorcentajeCorte, 3);
        Assert.Equal(48, resultado.BosquePorcentajeActual, 3);
        Assert.Equal(new DateOnly(2026, 10, 30), resultado.FechaImagenActual);
        Assert.Contains("\"type\":\"Polygon\"", manejador.CuerpoEnviado);
        Assert.Contains("\"cutoffDate\":\"2020-12-31\"", manejador.CuerpoEnviado);
    }

    // ---------- Proxy: caché del análisis satelital ----------

    [Fact]
    public async Task Proxy_NoRepiteElAnalisisSiElPoligonoNoCambio()
    {
        var real = new CoberturaContadora();
        var proxy = new CoberturaCacheProxy(real, new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromDays(7));
        var corte = new DateOnly(2020, 12, 31);

        await proxy.AnalizarAsync(Ayudas.Cuadrado(), corte);
        await proxy.AnalizarAsync(Ayudas.Cuadrado(), corte);
        Assert.Equal(1, real.Llamadas);

        var movido = Ayudas.Cuadrado();
        movido[1] = new Coordenada(movido[1].Lon + 0.0001, movido[1].Lat);
        await proxy.AnalizarAsync(movido, corte);
        Assert.Equal(2, real.Llamadas);
    }

    // ---------- Decorator: bitácora de auditoría ----------

    [Fact]
    public void Decorator_RegistraLasOperacionesCorrectasYLasRechazadas()
    {
        var predios = new RepositorioMemoria<Predio>();
        var productores = new RepositorioMemoria<Productor>();
        productores.Agregar(new Productor { Nombre = "Juan", Apellido = "Pérez", Ci = "7845123" });
        var registros = new RepositorioMemoria<RegistroBitacora>();
        var bitacora = new Bitacora(registros, new UsuarioFijo("mauricio"));

        var real = new ServicioPredios(predios, productores, new RepositorioMemoria<Poligono>(),
            new RepositorioMemoria<Documento>(), new RepositorioMemoria<Alerta>());
        IServicioPredios servicio = new ServicioPrediosAuditado(real, new AuditorOperaciones(bitacora));

        var predio = servicio.Crear(new PredioDto(1, "El Algarrobo", "San Julián", 100, null));
        Assert.Throws<OperacionNoPermitidaException>(() => servicio.Aprobar(predio.Id, null));

        var lista = bitacora.Listar();
        Assert.Equal(2, lista.Count);
        Assert.All(lista, r => Assert.Equal("mauricio", r.Usuario));
        Assert.Contains(lista, r => r.Operacion == "Crear predio" && r.Resultado == "Correcto" && r.EntidadId == predio.Id.ToString());
        Assert.Contains(lista, r => r.Operacion == "Aprobar predio" && r.Resultado.StartsWith("Rechazado") && r.ValoresAnteriores != null);
    }

    // ---------- Facade: validación de predios ----------

    private static (IServicioValidacionPredios Fachada, IRepositorio<Predio> Predios, IRepositorio<Alerta> Alertas) ArmarFachada(
        IProveedorCobertura cobertura, Predio predio, List<Coordenada> vertices, params Documento[] documentos)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton(typeof(IRepositorio<>), typeof(RepositorioMemoria<>));
        servicios.AddSingleton<IRepositorioParametros, RepositorioParametrosMemoria>();
        servicios.AddSingleton<IServicioParametros, ServicioParametros>();
        servicios.AddSingleton<IUsuarioActual>(new UsuarioFijo());
        servicios.AddSingleton<IBitacora, Bitacora>();
        servicios.AddSingleton(cobertura);
        servicios.AddSingleton<EstrategiaAutorizacionAbt>();
        servicios.AddSingleton<EstrategiaExencionLey741>();
        servicios.AddSingleton<SelectorEstrategiaUsoSuelo>();
        servicios.AddTransient<ValidadorCapa, ValidadorCobertura>();
        servicios.AddTransient<ValidadorCapa, ValidadorGeometrico>();
        servicios.AddTransient<ValidadorCapa, ValidadorUsoSuelo>();
        servicios.AddTransient<ValidadorCapa, ValidadorTitularidad>();
        servicios.AddTransient<IServicioValidacionPredios, ServicioValidacionPredios>();
        var proveedor = servicios.BuildServiceProvider();

        proveedor.GetRequiredService<IRepositorio<Productor>>().Agregar(new Productor { Nombre = "Juan", Apellido = "Pérez", Ci = "7845123" });
        var repoPredios = proveedor.GetRequiredService<IRepositorio<Predio>>();
        repoPredios.Agregar(predio);
        proveedor.GetRequiredService<IRepositorio<Poligono>>().Agregar(new Poligono { PredioId = predio.Id, Vertices = vertices });
        var repoDocumentos = proveedor.GetRequiredService<IRepositorio<Documento>>();
        foreach (var d in documentos)
        {
            d.PredioId = predio.Id;
            repoDocumentos.Agregar(d);
        }

        return (proveedor.GetRequiredService<IServicioValidacionPredios>(), repoPredios, proveedor.GetRequiredService<IRepositorio<Alerta>>());
    }

    [Fact]
    public async Task Facade_SinAlertasElPredioQuedaAprobado()
    {
        var (fachada, predios, alertas) = ArmarFachada(new CoberturaContadora(60, 58), Ayudas.Predio(), Ayudas.Cuadrado(),
            new Documento { Tipo = TipoDocumento.TituloPropiedad, TitularCi = "7845123", SuperficieHa = 120 },
            new Documento { Tipo = TipoDocumento.PlanOrdenamientoPredial, SuperficieHa = 120 });

        var resultado = await fachada.EnviarARevisionAsync(1);

        Assert.Equal(EstadoExpediente.Aprobado, resultado.EstadoResultante);
        Assert.Equal(EstadoExpediente.Aprobado, predios.Obtener(1)!.Estado);
        Assert.Equal(new[] { CapaValidacion.Geometrica, CapaValidacion.Titularidad, CapaValidacion.UsoSuelo, CapaValidacion.Cobertura },
            resultado.Capas.Select(c => c.Capa));
        Assert.Empty(alertas.Listar());
    }

    [Fact]
    public async Task Facade_ConAlertasElPredioPasaARevision()
    {
        var (fachada, predios, alertas) = ArmarFachada(new CoberturaContadora(70, 40), Ayudas.Predio(), Ayudas.Cuadrado());

        var resultado = await fachada.EnviarARevisionAsync(1);

        Assert.Equal(EstadoExpediente.EnRevision, predios.Obtener(1)!.Estado);
        Assert.Equal(3, alertas.Listar().Count);
        Assert.Contains(alertas.Listar(), a => a.Capa == CapaValidacion.Cobertura && a.Prioridad == PrioridadAlerta.Alta);
        Assert.Equal(4, resultado.Capas.Count);
    }
}
