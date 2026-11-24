using System.Net;
using System.Text;
using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
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
            new RepositorioMemoria<Documento>(), new ControlAcceso(new UsuarioFijo()));
        IServicioPredios servicio = new ServicioPrediosAuditado(real, new AuditorOperaciones(bitacora));

        var predio = servicio.Crear(new PredioDto(1, "El Algarrobo", "San Julián", "Soya", 100, null, null, Departamento: "Santa Cruz"));
        servicio.Obtener(predio.Id).EnviarARevision(hayAlertas: false);
        Assert.Throws<OperacionNoPermitidaException>(() => servicio.Eliminar(predio.Id));

        var lista = bitacora.Listar();
        Assert.Equal(2, lista.Count);
        Assert.All(lista, r => Assert.Equal("mauricio", r.Usuario));
        Assert.Contains(lista, r => r.Operacion == "Crear predio" && r.Resultado == "Correcto" && r.EntidadId == predio.Id.ToString());
        Assert.Contains(lista, r => r.Operacion == "Eliminar predio" && r.Resultado.StartsWith("Rechazado") && r.ValoresAnteriores != null);
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
        servicios.AddSingleton<ControlAcceso>();
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
        var (fachada, predios, alertas) = ArmarFachada(new CoberturaContadora(60, 58), Completo(Ayudas.Predio(desmonte: 0)), Ayudas.Cuadrado(),
            new Documento { Tipo = TipoDocumento.TituloPropiedad, Numero = "TIT-001", TitularCi = "7845123", SuperficieHa = 120 },
            new Documento { Tipo = TipoDocumento.PlanOrdenamientoPredial, SuperficieHa = 120 });

        var resultado = await fachada.EnviarARevisionAsync(1);

        Assert.Equal(EstadoExpediente.Aprobado, resultado.EstadoResultante);
        Assert.Equal(EstadoExpediente.Aprobado, predios.Obtener(1)!.Estado);
        Assert.Equal(new[] { CapaValidacion.Geometrica, CapaValidacion.Titularidad, CapaValidacion.UsoSuelo, CapaValidacion.Cobertura },
            resultado.Capas.Select(c => c.Capa));
        Assert.Empty(alertas.Listar());
    }

    [Fact]
    public async Task Facade_ConAlertasElPredioQuedaConAlertas()
    {
        // Requisitos completos, pero el título es de otra persona y la cobertura bajó del 70 % al 40 %.
        var (fachada, predios, alertas) = ArmarFachada(new CoberturaContadora(70, 40), Completo(Ayudas.Predio(desmonte: 0)), Ayudas.Cuadrado(),
            new Documento { Tipo = TipoDocumento.TituloPropiedad, Numero = "TIT-001", TitularCi = "1111111", SuperficieHa = 120 });

        var resultado = await fachada.EnviarARevisionAsync(1);

        Assert.Equal(EstadoExpediente.ConAlertas, predios.Obtener(1)!.Estado);
        Assert.Equal(2, alertas.Listar().Count);
        Assert.Contains(alertas.Listar(), a => a.Capa == CapaValidacion.Titularidad);
        Assert.Contains(alertas.Listar(), a => a.Capa == CapaValidacion.Cobertura && a.Prioridad == PrioridadAlerta.Alta);
        Assert.Equal(4, resultado.Capas.Count);
    }

    private static Predio Completo(Predio p)
    {
        p.Cultivo = "Soya";
        p.NumeroTituloInra = "TIT-001";
        return p;
    }

    [Fact]
    public async Task Requisitos_IncompletosBloqueanElEnvioYNoGuardanNada()
    {
        var (fachada, predios, alertas) = ArmarFachada(new CoberturaContadora(), Ayudas.Predio(), Ayudas.Cuadrado());

        var revision = fachada.RevisarRequisitos(1);
        Assert.False(revision.Completo);
        Assert.Equal(new[] { "datos", "titulo", "desmonte" }, revision.Pendientes.Select(r => r.Clave));

        var error = await Assert.ThrowsAsync<DatosInvalidosException>(() => fachada.EnviarARevisionAsync(1));
        Assert.Contains("faltan requisitos", error.Message);
        Assert.Equal(EstadoExpediente.Registrado, predios.Obtener(1)!.Estado);
        Assert.Empty(alertas.Listar());
        Assert.Empty(fachada.Historial(1));
    }

    [Fact]
    public void Requisitos_FueraDeLaLey741ExigenPopYPlanDeDesmonte()
    {
        // 100 ha de predio y 30 ha de desmonte: no entra en la exención (50 ha de predio o 20 ha de desmonte).
        var (fachada, _, _) = ArmarFachada(new CoberturaContadora(), Completo(Ayudas.Predio(desmonte: 30)), Ayudas.Cuadrado(),
            new Documento { Tipo = TipoDocumento.TituloPropiedad, Numero = "TIT-001", TitularCi = "7845123", SuperficieHa = 120 });

        Assert.Equal(new[] { "pop", "plan-desmonte" }, fachada.RevisarRequisitos(1).Pendientes.Select(r => r.Clave));
    }

    [Fact]
    public void Requisitos_UnPopOPlanSinNumeroNiSuperficieNoCumplen()
    {
        // Registros vacíos (por ejemplo, cargados antes de esta validación): existen, pero no cuentan como requisito.
        var (fachada, _, _) = ArmarFachada(new CoberturaContadora(), Completo(Ayudas.Predio(desmonte: 30)), Ayudas.Cuadrado(),
            new Documento { Tipo = TipoDocumento.TituloPropiedad, Numero = "TIT-001", TitularCi = "7845123", SuperficieHa = 120 },
            new Documento { Tipo = TipoDocumento.PlanOrdenamientoPredial },
            new Documento { Tipo = TipoDocumento.PlanDesmonte, Numero = "PD-001" });

        Assert.Equal(new[] { "pop", "plan-desmonte" }, fachada.RevisarRequisitos(1).Pendientes.Select(r => r.Clave));
    }

    [Fact]
    public void Documentos_NoSeAceptanSinNumeroNiSuperficie()
    {
        var predios = new RepositorioMemoria<Predio>();
        var predio = predios.Agregar(Ayudas.Predio(1, 100));
        var servicio = new ServicioDocumentos(predios, new RepositorioMemoria<Documento>(), new ControlAcceso(new UsuarioFijo()));

        var sinNumero = Assert.Throws<DatosInvalidosException>(() =>
            servicio.Agregar(predio.Id, new DocumentoDto(TipoDocumento.PlanOrdenamientoPredial, null, null, 100)));
        Assert.Contains("número del Plan de Ordenamiento Predial", sinNumero.Message);

        var sinSuperficie = Assert.Throws<DatosInvalidosException>(() =>
            servicio.Agregar(predio.Id, new DocumentoDto(TipoDocumento.PlanDesmonte, "PD-001", null, null)));
        Assert.Contains("superficie autorizada", sinSuperficie.Message);

        var pop = servicio.Agregar(predio.Id, new DocumentoDto(TipoDocumento.PlanOrdenamientoPredial, "POP-001", null, 100));
        Assert.Equal("POP-001", pop.Numero);
    }

    [Fact]
    public void Requisitos_DentroDeLaLey741NoPidenDocumentosDeLaAbt()
    {
        var (fachada, _, _) = ArmarFachada(new CoberturaContadora(), Completo(Ayudas.Predio(desmonte: 10)), Ayudas.Cuadrado(),
            new Documento { Tipo = TipoDocumento.TituloPropiedad, Numero = "TIT-001", TitularCi = "7845123", SuperficieHa = 120 });

        var revision = fachada.RevisarRequisitos(1);
        Assert.True(revision.Completo);
        Assert.DoesNotContain(revision.Requisitos, r => r.Clave is "pop" or "plan-desmonte");
    }

    [Fact]
    public void Requisitos_UnPuntoNoAlcanzaConMasDe4Hectareas()
    {
        var (fachada, _, _) = ArmarFachada(new CoberturaContadora(), Completo(Ayudas.Predio(superficieHa: 4.5, desmonte: 0)),
            [new Coordenada(-63.3, -17.6)],
            new Documento { Tipo = TipoDocumento.TituloPropiedad, Numero = "TIT-001", TitularCi = "7845123", SuperficieHa = 5 });

        var ubicacion = fachada.RevisarRequisitos(1).Requisitos.Single(r => r.Clave == "ubicacion");
        Assert.False(ubicacion.Cumplido);
        Assert.Contains("se exige el polígono", ubicacion.Detalle);
    }

    [Fact]
    public void Predio_ElTituloSeRegistraJuntoConElPredio()
    {
        var predios = new RepositorioMemoria<Predio>();
        var documentos = new RepositorioMemoria<Documento>();
        var productores = new RepositorioMemoria<Productor>();
        productores.Agregar(new Productor { Nombre = "Juan", Apellido = "Pérez", Ci = "7845123" });
        var servicio = new ServicioPredios(predios, productores, new RepositorioMemoria<Poligono>(), documentos,
            new ControlAcceso(new UsuarioFijo()));

        var predio = servicio.Crear(new PredioDto(1, "El Algarrobo", "San Julián", "Soya", 100, "TIT-001", 0, "7845123", 100, "Santa Cruz"));
        var titulo = documentos.Listar(_ => true).Single();
        Assert.Equal(("TIT-001", "7845123", (double?)100), (titulo.Numero, titulo.TitularCi, titulo.SuperficieHa));

        // Al editar el predio se actualiza el mismo documento, no se crea otro.
        servicio.Actualizar(predio.Id, new PredioDto(1, "El Algarrobo", "San Julián", "Soya", 100, "TIT-002", 0, "7845123", 90, "Santa Cruz"));
        titulo = documentos.Listar(_ => true).Single();
        Assert.Equal(("TIT-002", (double?)90), (titulo.Numero, titulo.SuperficieHa));

        Assert.Throws<DatosInvalidosException>(() =>
            servicio.Crear(new PredioDto(1, "Otro", "San Julián", "Soya", 10, null, 0, "7845123", 10, "Santa Cruz")));

        // Sin título se puede registrar el predio aunque el CI venga precargado: el título se agrega después.
        servicio.Crear(new PredioDto(1, "Sin título", "San Julián", "Soya", 10, null, 0, "7845123", null, "Santa Cruz"));
        Assert.Single(documentos.Listar(_ => true));
    }

    [Fact]
    public void Predio_ElMunicipioDebeSerDelDepartamento()
    {
        var predios = new RepositorioMemoria<Predio>();
        var productores = new RepositorioMemoria<Productor>();
        productores.Agregar(new Productor { Nombre = "Juan", Apellido = "Pérez", Ci = "7845123" });
        var servicio = new ServicioPredios(predios, productores, new RepositorioMemoria<Poligono>(),
            new RepositorioMemoria<Documento>(), new ControlAcceso(new UsuarioFijo()));
        PredioDto Dto(string? departamento, string municipio) =>
            new(1, "El Algarrobo", municipio, "Café", 10, null, 0, Departamento: departamento);

        // Sin importar mayúsculas ni tildes, se guarda el nombre oficial.
        var predio = servicio.Crear(Dto("la paz", "caranavi"));
        Assert.Equal(("La Paz", "Caranavi"), (predio.Departamento, predio.Municipio));
        predio = servicio.Crear(Dto("Santa Cruz", "san julian"));
        Assert.Equal("San Julián", predio.Municipio);

        var error = Assert.Throws<DatosInvalidosException>(() => servicio.Crear(Dto("Beni", "San Julián")));
        Assert.Contains("no es un municipio de Beni", error.Message);
        Assert.Throws<DatosInvalidosException>(() => servicio.Crear(Dto("Atlántida", "Caranavi")));
        Assert.Throws<DatosInvalidosException>(() => servicio.Crear(Dto(null, "Caranavi")));
    }

    [Fact]
    public void DivisionPolitica_TieneLos340MunicipiosDeBolivia()
    {
        Assert.Equal(9, DivisionPolitica.Departamentos.Count());
        Assert.Equal(340, DivisionPolitica.Municipios.Values.Sum(m => m.Length));
        Assert.Equal(56, DivisionPolitica.Municipios["Santa Cruz"].Length);
    }

    [Fact]
    public void Documentos_ElTituloSeRegistraTambienDesdeDocumentosYQuedaEnElPredio()
    {
        var predios = new RepositorioMemoria<Predio>();
        var documentos = new RepositorioMemoria<Documento>();
        var predio = predios.Agregar(Ayudas.Predio(1, 100));
        var servicio = new ServicioDocumentos(predios, documentos, new ControlAcceso(new UsuarioFijo()));

        var titulo = servicio.Agregar(predio.Id, new DocumentoDto(TipoDocumento.TituloPropiedad, "TIT-010", "7845123", 100));
        Assert.Equal("TIT-010", predios.Obtener(predio.Id)!.NumeroTituloInra);

        // Un solo título por predio.
        Assert.Throws<DatosInvalidosException>(() =>
            servicio.Agregar(predio.Id, new DocumentoDto(TipoDocumento.TituloPropiedad, "TIT-011", "7845123", 100)));

        servicio.Eliminar(titulo.Id);
        Assert.True(predios.Obtener(predio.Id)!.NumeroTituloInra is null);
    }
}
