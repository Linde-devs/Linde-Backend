using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Expedientes;
using LINDE.Api.Servicios;

namespace LINDE.Tests;

/// <summary>RF-14: inspección de campo que registra el productor con el resultado del técnico.</summary>
public class InspeccionesTests
{
    private static readonly DateOnly Ayer = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4)).AddDays(-1);

    private static (ServicioInspecciones Servicio, RepositorioMemoria<Predio> Predios, RepositorioMemoria<InspeccionCampo> Inspecciones)
        Crear(IUsuarioActual? usuario = null)
    {
        usuario ??= new UsuarioFijo("juan@correo.bo", RolUsuario.Productor, productorId: 1);
        var predios = new RepositorioMemoria<Predio>();
        var inspecciones = new RepositorioMemoria<InspeccionCampo>();
        return (new ServicioInspecciones(predios, inspecciones, new ControlAcceso(usuario), usuario), predios, inspecciones);
    }

    [Fact]
    public void Inspeccion_SeRegistraConFechaTecnicoResultadoYQuienLaCargo()
    {
        var (servicio, predios, _) = Crear();
        var predio = predios.Agregar(Ayudas.Predio());

        var i = servicio.Registrar(predio.Id, new InspeccionDto(Ayer, "  Ing. Carla Rojas ", ResultadoInspeccion.Conforme, " "));

        Assert.Equal("Ing. Carla Rojas", i.Tecnico);
        Assert.Null(i.Comentario);
        Assert.Equal("juan@correo.bo", i.RegistradaPor);
        Assert.Single(servicio.Listar(predio.Id));
    }

    [Theory]
    [InlineData("fecha")]
    [InlineData("futura")]
    [InlineData("tecnico")]
    [InlineData("resultado")]
    [InlineData("comentario")]
    public void Inspeccion_SeRechazaConDatosIncompletos(string caso)
    {
        var (servicio, predios, _) = Crear();
        var predio = predios.Agregar(Ayudas.Predio());
        var dto = caso switch
        {
            "fecha" => new InspeccionDto(null, "Ing. Rojas", ResultadoInspeccion.Conforme, null),
            "futura" => new InspeccionDto(Ayer.AddDays(3), "Ing. Rojas", ResultadoInspeccion.Conforme, null),
            "tecnico" => new InspeccionDto(Ayer, " ", ResultadoInspeccion.Conforme, null),
            "resultado" => new InspeccionDto(Ayer, "Ing. Rojas", null, null),
            _ => new InspeccionDto(Ayer, "Ing. Rojas", ResultadoInspeccion.NoConforme, null)
        };

        var error = Assert.Throws<DatosInvalidosException>(() => servicio.Registrar(predio.Id, dto));
        Assert.Equal(caso == "futura" ? "fecha" : caso, error.Campo);
    }

    [Fact]
    public void Inspeccion_UnProductorNoVeNiCargaInspeccionesDePrediosAjenos()
    {
        var (servicio, predios, _) = Crear();
        var ajeno = Ayudas.Predio();
        ajeno.ProductorId = 2;
        predios.Agregar(ajeno);

        Assert.Throws<AccesoDenegadoException>(() => servicio.Listar(ajeno.Id));
        Assert.Throws<AccesoDenegadoException>(() =>
            servicio.Registrar(ajeno.Id, new InspeccionDto(Ayer, "Ing. Rojas", ResultadoInspeccion.Conforme, null)));
    }

    [Fact]
    public void Inspeccion_ElDecoratorAnotaElRegistroYElBorrado()
    {
        var (real, predios, inspecciones) = Crear();
        var predio = predios.Agregar(Ayudas.Predio());
        var bitacora = new Bitacora(new RepositorioMemoria<RegistroBitacora>(), new UsuarioFijo("juan@correo.bo"));
        IServicioInspecciones servicio = new ServicioInspeccionesAuditado(real, inspecciones, new AuditorOperaciones(bitacora));

        var i = servicio.Registrar(predio.Id, new InspeccionDto(Ayer, "Ing. Rojas", ResultadoInspeccion.ConObservaciones, "Falta un mojón"));
        servicio.Eliminar(i.Id);

        var lista = bitacora.Listar("InspeccionCampo");
        Assert.Equal(2, lista.Count);
        Assert.Contains(lista, r => r.Operacion.StartsWith("Registrar inspección") && r.EntidadId == i.Id.ToString());
        Assert.Contains(lista, r => r.Operacion == "Eliminar inspección de campo" && r.ValoresAnteriores!.Contains("Ing. Rojas"));
        Assert.Empty(servicio.Listar(predio.Id));
    }

    [Fact]
    public void Builder_IncluyeLasInspeccionesEnElExpediente()
    {
        var predio = Ayudas.Predio();
        predio.EnviarARevision(hayAlertas: false);
        var inspecciones = new[]
        {
            new InspeccionCampo { PredioId = predio.Id, Fecha = Ayer.AddDays(-10), Tecnico = "Ing. Rojas", Resultado = ResultadoInspeccion.Conforme },
            new InspeccionCampo { PredioId = predio.Id, Fecha = Ayer, Tecnico = "Ing. Vaca", Resultado = ResultadoInspeccion.ConObservaciones, Comentario = "Mojón movido" }
        };

        var expediente = new ExpedienteBuilder()
            .ConProductor(new Productor { Nombre = "Juan", Apellido = "Pérez", Ci = "7845123" })
            .ConPredio(predio)
            .ConPoligono(new Poligono { PredioId = predio.Id, Vertices = Ayudas.Cuadrado() })
            .ConValidacion(new ResultadoValidacion { PredioId = predio.Id })
            .ConInspecciones(inspecciones)
            .Construir("LINDE-2026-000001");

        Assert.Equal(2, expediente.Inspecciones.Count);
        Assert.Equal("Ing. Vaca", expediente.Inspecciones[0].Tecnico);
        Assert.Contains("\"tecnico\": \"Ing. Vaca\"", FormatosExpediente.AGeoJson(expediente));
    }
}
