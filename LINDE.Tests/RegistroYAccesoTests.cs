using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LINDE.Tests;

/// <summary>Sprint 2: registro de productores (RF-01), predios (RF-02) y acceso por rol (RF-11, RNF-03).</summary>
public class RegistroYAccesoTests
{
    /// <summary>Repositorios compartidos, como en la API, y servicios armados para un usuario dado.</summary>
    private sealed class Entorno
    {
        public RepositorioMemoria<Usuario> Usuarios { get; } = new();
        public RepositorioMemoria<Productor> Productores { get; } = new();
        public RepositorioMemoria<Predio> Predios { get; } = new();
        public RepositorioMemoria<Poligono> Poligonos { get; } = new();

        public ServicioProductores ServicioProductores()
        {
            var opciones = Options.Create(new OpcionesJwt { Clave = "clave-de-prueba-con-mas-de-32-caracteres-123" });
            var cuentas = new ServicioAutenticacion(Usuarios, new PasswordHasher<Usuario>(), new GeneradorTokenJwt(opciones));
            return new ServicioProductores(Productores, Predios, Usuarios, cuentas);
        }

        public ServicioPredios ServicioPredios(UsuarioFijo usuario) =>
            new(Predios, Productores, Poligonos, new RepositorioMemoria<Documento>(), new ControlAcceso(usuario));

        public ServicioPoligonos ServicioPoligonos(UsuarioFijo usuario) =>
            new(Predios, Poligonos, new FabricaLectores([new LectorGeoJson(), new LectorKml()]), usuario, new ControlAcceso(usuario));
    }

    private static RegistroProductorDto Registro(string ci = "7845123", string correo = "juan@correo.bo") =>
        new("Juan", "Pérez", ci, null, "70012345", "San Julián", correo, "linde2026");

    private static PredioDto Predio(int? productorId = null, double superficie = 120.5) =>
        new(productorId, "El Algarrobo", "San Julián", "Soya", superficie, "SPP-NAL-070167", null, Departamento: "Santa Cruz");

    private static UsuarioFijo ComoProductor(ProductorVista p) => new(p.Correo, RolUsuario.Productor, p.Id);

    [Fact]
    public void Registro_AsignaCodigoUnicoYCreaLaCuentaDeProductor()
    {
        var e = new Entorno();

        var primero = e.ServicioProductores().Registrar(Registro());
        var segundo = e.ServicioProductores().Registrar(Registro(ci: "1234567", correo: "ana@correo.bo"));

        Assert.Equal("PRD-000001", primero.Codigo);
        Assert.Equal("PRD-000002", segundo.Codigo);
        var cuenta = e.Usuarios.Listar(u => u.Correo == "juan@correo.bo").Single();
        Assert.Equal(RolUsuario.Productor, cuenta.Rol);
        Assert.Equal(primero.Id, cuenta.ProductorId);
    }

    [Fact]
    public void Registro_CiRepetidoSeRechazaConProductorYaRegistrado()
    {
        var e = new Entorno();
        e.ServicioProductores().Registrar(Registro());

        var error = Assert.Throws<DatosInvalidosException>(() =>
            e.ServicioProductores().Registrar(Registro(correo: "otro@correo.bo")));

        Assert.Equal("Productor ya registrado", error.Message);
        Assert.Equal("ci", error.Campo);
        Assert.Single(e.Usuarios.Listar());
    }

    [Fact]
    public void Registro_ConCorreoRepetidoNoDejaUnProductorAMedias()
    {
        var e = new Entorno();
        e.ServicioProductores().Registrar(Registro());

        var error = Assert.Throws<DatosInvalidosException>(() =>
            e.ServicioProductores().Registrar(Registro(ci: "1234567")));

        Assert.Equal("correo", error.Campo);
        Assert.Single(e.Productores.Listar());
    }

    [Fact]
    public void Predio_SinSuperficieOConMasDeDosDecimalesNoSeGuarda()
    {
        var e = new Entorno();
        var productor = e.ServicioProductores().Registrar(Registro());
        var servicio = e.ServicioPredios(ComoProductor(productor));

        Assert.Equal("superficieDeclaradaHa", Assert.Throws<DatosInvalidosException>(() => servicio.Crear(Predio(superficie: 0))).Campo);
        Assert.Equal("superficieDeclaradaHa", Assert.Throws<DatosInvalidosException>(() => servicio.Crear(Predio(superficie: 10.555))).Campo);

        var guardado = servicio.Crear(Predio(superficie: 10.55));
        Assert.Equal(10.55, guardado.SuperficieDeclaradaHa);
    }

    [Fact]
    public void Predio_SinProductorNoSeGuarda()
    {
        var e = new Entorno();
        var admin = e.ServicioPredios(new UsuarioFijo());

        Assert.Equal("productorId", Assert.Throws<DatosInvalidosException>(() => admin.Crear(Predio(productorId: null))).Campo);
        Assert.Equal("productorId", Assert.Throws<DatosInvalidosException>(() => admin.Crear(Predio(productorId: 99))).Campo);
    }

    [Fact]
    public void Acceso_ElProductorSoloVeYModificaSusPredios()
    {
        var e = new Entorno();
        var juan = e.ServicioProductores().Registrar(Registro());
        var ana = e.ServicioProductores().Registrar(Registro(ci: "1234567", correo: "ana@correo.bo"));

        // Juan intenta registrar el predio a nombre de Ana: queda a su nombre.
        var deJuan = e.ServicioPredios(ComoProductor(juan)).Crear(Predio(productorId: ana.Id));
        Assert.Equal(juan.Id, deJuan.ProductorId);

        var servicioAna = e.ServicioPredios(ComoProductor(ana));
        Assert.Empty(servicioAna.Listar(productorId: juan.Id));
        Assert.Throws<AccesoDenegadoException>(() => servicioAna.Obtener(deJuan.Id));
        Assert.Throws<AccesoDenegadoException>(() => servicioAna.Eliminar(deJuan.Id));
        Assert.Throws<AccesoDenegadoException>(() =>
            e.ServicioPoligonos(ComoProductor(ana)).Registrar(deJuan.Id, Ayudas.Cuadrado(), null));

        // El administrador ve todos.
        Assert.Single(e.ServicioPredios(new UsuarioFijo()).Listar());
    }

    [Fact]
    public void Punto_SeGuardaConSeisDecimales()
    {
        var e = new Entorno();
        var juan = e.ServicioProductores().Registrar(Registro());
        var predio = e.ServicioPredios(ComoProductor(juan)).Crear(Predio(superficie: 3));

        var punto = e.ServicioPoligonos(ComoProductor(juan)).Registrar(predio.Id, [new Coordenada(-63.123456789, -17.987654321)], null);

        Assert.True(punto.EsPunto);
        Assert.Equal(new Coordenada(-63.123457, -17.987654), punto.Vertices[0]);
    }

    [Fact]
    public void Poligono_ConLadosCruzadosSeRechazaAlCargarloYNoSeGuarda()
    {
        var e = new Entorno();
        var juan = e.ServicioProductores().Registrar(Registro());
        var predio = e.ServicioPredios(ComoProductor(juan)).Crear(Predio());

        var error = Assert.Throws<GeometriaInvalidaException>(() =>
            e.ServicioPoligonos(ComoProductor(juan)).Registrar(predio.Id, Ayudas.Mono(), null));

        Assert.Equal(ErrorGeometria.Autointerseccion, error.Tipo);
        Assert.Empty(e.Poligonos.Listar());
    }
}
