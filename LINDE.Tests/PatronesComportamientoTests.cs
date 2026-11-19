using LINDE.Api.Cobertura;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using LINDE.Api.Validacion;
using LINDE.Api.Validacion.UsoSuelo;

namespace LINDE.Tests;

public class PatronesComportamientoTests
{
    private static List<ValidadorCapa> Validadores(IRepositorio<Poligono>? poligonos = null, IProveedorCobertura? cobertura = null) => new()
    {
        new ValidadorCobertura(cobertura ?? new CoberturaContadora()),
        new ValidadorUsoSuelo(new SelectorEstrategiaUsoSuelo(new EstrategiaAutorizacionAbt(), new EstrategiaExencionLey741())),
        new ValidadorGeometrico(poligonos ?? new RepositorioMemoria<Poligono>()),
        new ValidadorTitularidad()
    };

    // ---------- Chain of Responsibility ----------

    [Fact]
    public async Task Chain_RecorreLasCuatroCapasEnOrdenAunqueSeRegistrenDesordenadas()
    {
        var contexto = Ayudas.Contexto(Ayudas.Predio(), Ayudas.Cuadrado());

        await ValidadorCapa.ArmarCadena(Validadores()).ProcesarAsync(contexto);

        Assert.False(contexto.Cortado);
        Assert.Equal(new[] { CapaValidacion.Geometrica, CapaValidacion.Titularidad, CapaValidacion.UsoSuelo, CapaValidacion.Cobertura },
            contexto.Resultados.Select(r => r.Capa));
    }

    [Fact]
    public async Task Chain_LaCapaGeometricaCortaCuandoLosLadosSeCruzan()
    {
        var contexto = Ayudas.Contexto(Ayudas.Predio(), Ayudas.Mono());

        await ValidadorCapa.ArmarCadena(Validadores()).ProcesarAsync(contexto);

        Assert.True(contexto.Cortado);
        var unico = Assert.Single(contexto.Resultados);
        Assert.Equal(EstadoCapa.Rechazado, unico.Estado);
        Assert.Contains("se cruzan", unico.Mensaje);
    }

    [Fact]
    public async Task Chain_LasDemasCapasRegistranAlertasPeroNoCortan()
    {
        var contexto = Ayudas.Contexto(Ayudas.Predio(), Ayudas.Cuadrado());

        await ValidadorCapa.ArmarCadena(Validadores(cobertura: new CoberturaContadora(80, 30))).ProcesarAsync(contexto);

        Assert.False(contexto.Cortado);
        Assert.Equal(4, contexto.Resultados.Count);
        Assert.Equal(EstadoCapa.Alerta, contexto.Resultados[1].Estado);
        Assert.Equal(EstadoCapa.Alerta, contexto.Resultados[3].Estado);
    }

    [Fact]
    public async Task Chain_RechazaUnPuntoEnPrediosDeMasDe4Hectareas()
    {
        var contexto = Ayudas.Contexto(Ayudas.Predio(superficieHa: 10), new List<Coordenada> { new(-63, -17.5) });

        await ValidadorCapa.ArmarCadena(Validadores()).ProcesarAsync(contexto);

        Assert.True(contexto.Cortado);
    }

    [Fact]
    public async Task Chain_DetectaLaSuperposicionConOtroPredio()
    {
        var poligonos = new RepositorioMemoria<Poligono>();
        poligonos.Agregar(new Poligono { PredioId = 99, Vertices = Ayudas.Cuadrado(lon: -62.995) });
        var contexto = Ayudas.Contexto(Ayudas.Predio(), Ayudas.Cuadrado());

        await ValidadorCapa.ArmarCadena(Validadores(poligonos)).ProcesarAsync(contexto);

        Assert.True(contexto.Cortado);
        Assert.Contains("predio 99", contexto.Resultados[0].Mensaje);
    }

    // ---------- State ----------

    [Fact]
    public void State_SinAlertasPasaAAprobadoYConAlertasAEnRevision()
    {
        var limpio = Ayudas.Predio();
        limpio.EnviarARevision(hayAlertas: false);
        Assert.Equal(EstadoExpediente.Aprobado, limpio.Estado);

        var conAlertas = Ayudas.Predio();
        conAlertas.EnviarARevision(hayAlertas: true);
        Assert.Equal(EstadoExpediente.EnRevision, conAlertas.Estado);
    }

    [Fact]
    public void State_EnRevisionSoloActuaElVerificador()
    {
        var predio = Ayudas.Predio();
        predio.EnviarARevision(hayAlertas: true);

        Assert.Throws<OperacionNoPermitidaException>(predio.VerificarEdicion);
        Assert.Throws<OperacionNoPermitidaException>(predio.VerificarGeneracionExpediente);
        Assert.Throws<DatosInvalidosException>(() => predio.Observar(" "));

        predio.Observar("Revisar el límite norte");
        Assert.Equal(EstadoExpediente.Observado, predio.Estado);
    }

    [Fact]
    public void State_ObservadoSePuedeCorregirYReenviar()
    {
        var predio = Ayudas.Predio();
        predio.EnviarARevision(hayAlertas: true);
        predio.Observar("Falta el título");

        predio.VerificarEdicion();
        predio.EnviarARevision(hayAlertas: false);
        Assert.Equal(EstadoExpediente.Aprobado, predio.Estado);
    }

    [Fact]
    public void State_AprobadoEsFinalYSoloPermiteElExpediente()
    {
        var predio = Ayudas.Predio();
        predio.EnviarARevision(hayAlertas: false);

        predio.VerificarGeneracionExpediente();
        Assert.Throws<OperacionNoPermitidaException>(predio.VerificarEdicion);
        Assert.Throws<OperacionNoPermitidaException>(predio.VerificarEnvio);
        Assert.Throws<OperacionNoPermitidaException>(() => predio.Aprobar(null));
    }

    // ---------- Strategy ----------

    private static readonly SelectorEstrategiaUsoSuelo Selector = new(new EstrategiaAutorizacionAbt(), new EstrategiaExencionLey741());

    [Fact]
    public void Strategy_PrediosPequenosUsanLaExencionDeLaLey741()
    {
        var contexto = Ayudas.Contexto(Ayudas.Predio(superficieHa: 30), Ayudas.Cuadrado(lado: 0.005));
        contexto.AreaPoligonoHa = 30;

        var estrategia = Selector.Seleccionar(contexto);

        Assert.IsType<EstrategiaExencionLey741>(estrategia);
        Assert.Equal(EstadoUsoSuelo.NoRequiereAutorizacion, estrategia.Evaluar(contexto).Estado);
    }

    [Fact]
    public void Strategy_DesmonteDeHasta20HectareasTambienEstaExento()
    {
        var contexto = Ayudas.Contexto(Ayudas.Predio(superficieHa: 300, desmonte: 15), Ayudas.Cuadrado());
        contexto.AreaPoligonoHa = 300;

        Assert.IsType<EstrategiaExencionLey741>(Selector.Seleccionar(contexto));
    }

    [Fact]
    public void Strategy_PrediosGrandesSinPopNoEstanAutorizados()
    {
        var contexto = Ayudas.Contexto(Ayudas.Predio(superficieHa: 100), Ayudas.Cuadrado());
        contexto.AreaPoligonoHa = 100;

        var estrategia = Selector.Seleccionar(contexto);

        Assert.IsType<EstrategiaAutorizacionAbt>(estrategia);
        Assert.Equal(EstadoUsoSuelo.NoAutorizado, estrategia.Evaluar(contexto).Estado);
    }

    [Fact]
    public void Strategy_LosUmbralesSalenDeLosParametros()
    {
        var contexto = new ContextoValidacion
        {
            Predio = Ayudas.Predio(superficieHa: 100),
            Poligono = new Poligono { Vertices = Ayudas.Cuadrado() },
            Parametros = new Parametros { Ley741PredioMaximoHa = 150 },
            AreaPoligonoHa = 100
        };

        Assert.IsType<EstrategiaExencionLey741>(Selector.Seleccionar(contexto));
    }

    // ---------- Geometría ----------

    [Fact]
    public void Geometria_ElAreaSeCalculaEnMetrosYNoEnGrados()
    {
        var area = Geometrias.AreaHectareas(Ayudas.Cuadrado());
        Assert.InRange(area, 98, 102);
    }
}
