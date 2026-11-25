using System.IO.Compression;
using System.Text;
using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;
using LINDE.Api.Validacion;

namespace LINDE.Tests;

/// <summary>
/// Número y archivo del título del INRA (RF-02, RF-16) y corrección de un predio ya aprobado (patrón State).
/// </summary>
public class TitulosYEdicionTests
{
    // ---------- Número del título ----------

    [Theory]
    [InlineData("SPP-NAL-070167", "SPP-NAL-070167")]
    [InlineData("  spp nal 070167 ", "SPP-NAL-070167")]
    [InlineData("PPD-NAL 1050733", "PPD-NAL-1050733")]
    [InlineData("mpe_nal_005504", "MPE-NAL-005504")]
    public void NumeroTitulo_AceptaLaFormaDelInraYLaNormaliza(string escrito, string esperado)
    {
        var normalizado = NumeroTitulo.Normalizar(escrito);
        Assert.Equal(esperado, normalizado);
        Assert.True(NumeroTitulo.EsValido(normalizado));
    }

    [Theory]
    [InlineData("TIT-0214")]
    [InlineData("123456")]
    [InlineData("hola")]
    [InlineData("SPP-070167")]
    [InlineData("SPP-NAL-12")]
    public void NumeroTitulo_RechazaLoQueNoTieneLaForma(string escrito) =>
        Assert.False(NumeroTitulo.EsValido(NumeroTitulo.Normalizar(escrito)));

    private static (ServicioPredios Servicio, RepositorioMemoria<Predio> Predios, RepositorioMemoria<Documento> Documentos) Predios()
    {
        var productores = new RepositorioMemoria<Productor>();
        productores.Agregar(new Productor { Nombre = "Juan", Apellido = "Pérez", Ci = "7845123" });
        productores.Agregar(new Productor { Nombre = "Ana", Apellido = "Rojas", Ci = "5512345" });
        var predios = new RepositorioMemoria<Predio>();
        var documentos = new RepositorioMemoria<Documento>();
        var servicio = new ServicioPredios(predios, productores, new RepositorioMemoria<Poligono>(), documentos,
            new ControlAcceso(new UsuarioFijo()));
        return (servicio, predios, documentos);
    }

    private static PredioDto Dto(int productorId, string? titulo, string nombre = "El Algarrobo") =>
        new(productorId, nombre, "San Julián", "Soya", 100, titulo, 0, "7845123", titulo is null ? null : 120, "Santa Cruz");

    [Fact]
    public void Predio_ConNumeroDeTituloInventadoNoSeGuarda()
    {
        var (servicio, _, _) = Predios();
        var error = Assert.Throws<DatosInvalidosException>(() => servicio.Crear(Dto(1, "cualquier cosa")));
        Assert.Contains(NumeroTitulo.Ejemplo, error.Message);
    }

    [Fact]
    public void Predio_ElNumeroSeGuardaNormalizadoTambienEnSuDocumento()
    {
        var (servicio, _, documentos) = Predios();
        var predio = servicio.Crear(Dto(1, "spp nal 070167"));
        Assert.Equal("SPP-NAL-070167", predio.NumeroTituloInra);
        Assert.Equal("SPP-NAL-070167", documentos.Listar().Single().Numero);
    }

    [Fact]
    public void Predio_UnTituloDeOtroProductorSeRechaza_DelMismoProductorSePermite()
    {
        var (servicio, _, _) = Predios();
        servicio.Crear(Dto(1, "SPP-NAL-070167"));

        servicio.Crear(Dto(1, "SPP-NAL-070167", "Lote 2 del mismo título"));
        var error = Assert.Throws<DatosInvalidosException>(() => servicio.Crear(Dto(2, "SPP-NAL-070167", "Predio de Ana")));
        Assert.Contains("otro productor", error.Message);
    }

    [Fact]
    public void Documento_ElTituloAgregadoComoDocumentoTambienSeValida()
    {
        var (predios, _, _) = Predios();
        var predio = predios.Crear(Dto(1, null));
        var documentos = new RepositorioMemoria<Documento>();
        var repoPredios = new RepositorioMemoria<Predio>();
        repoPredios.Agregar(predio);
        var servicio = new ServicioDocumentos(repoPredios, documentos, new ControlAcceso(new UsuarioFijo()));

        Assert.Throws<DatosInvalidosException>(() =>
            servicio.Agregar(predio.Id, new DocumentoDto(TipoDocumento.TituloPropiedad, "ABC-1", "7845123", 120)));
        var titulo = servicio.Agregar(predio.Id, new DocumentoDto(TipoDocumento.TituloPropiedad, "ppd nal 086678", "7845123", 120));
        Assert.Equal("PPD-NAL-086678", titulo.Numero);
    }

    // ---------- Corregir un predio aprobado ----------

    [Fact]
    public void Aprobado_AlCambiarUnDatoVuelveARegistrado_GuardarSinCambiosNoLeQuitaLaAprobacion()
    {
        var (servicio, predios, _) = Predios();
        var predio = servicio.Crear(Dto(1, "SPP-NAL-070167"));
        predio.EnviarARevision(hayAlertas: false);

        servicio.Actualizar(predio.Id, Dto(1, "SPP-NAL-070167"));
        Assert.Equal(EstadoExpediente.Aprobado, predios.Obtener(predio.Id)!.Estado);

        servicio.Actualizar(predio.Id, Dto(1, "SPP-NAL-070167") with { SuperficieDeclaradaHa = 95 });
        Assert.Equal(EstadoExpediente.Registrado, predios.Obtener(predio.Id)!.Estado);
    }

    [Fact]
    public void Aprobado_SePuedeEliminar()
    {
        var (servicio, predios, _) = Predios();
        var predio = servicio.Crear(Dto(1, null));
        predio.EnviarARevision(hayAlertas: false);

        servicio.Eliminar(predio.Id);
        Assert.Null(predios.Obtener(predio.Id));
    }

    [Fact]
    public void Aprobado_UnPoligonoNuevoLoDevuelveARegistrado()
    {
        var predios = new RepositorioMemoria<Predio>();
        var predio = predios.Agregar(Ayudas.Predio());
        predio.EnviarARevision(hayAlertas: false);
        var servicio = new ServicioPoligonos(predios, new RepositorioMemoria<Poligono>(),
            new FabricaLectores([new LectorGeoJson(), new LectorKml()]), new UsuarioFijo(), new ControlAcceso(new UsuarioFijo()));

        servicio.Registrar(predio.Id, Ayudas.Cuadrado(), "Corrijo el lindero norte");

        Assert.Equal(EstadoExpediente.Registrado, predios.Obtener(predio.Id)!.Estado);
    }

    // ---------- Archivo del título ----------

    /// <summary>PDF mínimo con el contenido comprimido con FlateDecode, como los que generan Word o un sistema.</summary>
    private static byte[] Pdf(string texto)
    {
        var contenido = Encoding.Latin1.GetBytes($"BT /F1 12 Tf 72 720 Td (ESTADO PLURINACIONAL DE BOLIVIA) Tj T* " +
                                                 $"[(TITULO EJECUTORIAL ) -20 ({texto})] TJ ET");
        using var comprimido = new MemoryStream();
        using (var z = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true)) z.Write(contenido);
        var cuerpo = comprimido.ToArray();

        using var pdf = new MemoryStream();
        void Escribir(string s) => pdf.Write(Encoding.Latin1.GetBytes(s));
        Escribir("%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\n");
        Escribir($"4 0 obj << /Length {cuerpo.Length} /Filter /FlateDecode >>\nstream\n");
        pdf.Write(cuerpo);
        Escribir("\nendstream\nendobj\n%%EOF\n");
        return pdf.ToArray();
    }

    [Fact]
    public void TextoPdf_LeeElTextoComprimido()
    {
        var texto = TextoPdf.Extraer(Pdf("N.º SPP-NAL-070167 a favor de Juan Pérez"));
        Assert.Contains("SPP-NAL-070167", texto);
        Assert.True(TextoPdf.EsLegible(texto));
    }

    private static Documento ConArchivo(AlmacenArchivosMemoria almacen, byte[] datos, string tipo) => new()
    {
        Tipo = TipoDocumento.TituloPropiedad, Numero = "SPP-NAL-070167", TitularCi = "7845123", SuperficieHa = 120,
        ArchivoClave = almacen.Guardar(new MemoryStream(datos), tipo == "application/pdf" ? ".pdf" : ".jpg"), ArchivoTipo = tipo
    };

    private static async Task<ResultadoCapa> Titularidad(Documento titulo, AlmacenArchivosMemoria almacen)
    {
        var contexto = Ayudas.Contexto(Ayudas.Predio(), Ayudas.Cuadrado(), titulo);
        contexto.AreaPoligonoHa = 100;
        await new ValidadorTitularidad(null, new VerificacionArchivoTitulo(almacen)).ProcesarAsync(contexto);
        return contexto.Resultados.Single();
    }

    [Fact]
    public async Task ArchivoTitulo_PdfConElNumero_EsConforme()
    {
        var almacen = new AlmacenArchivosMemoria();
        var r = await Titularidad(ConArchivo(almacen, Pdf("N.º SPP-NAL-070167 a favor de Juan Pérez"), "application/pdf"), almacen);
        Assert.Equal(EstadoCapa.Conforme, r.Estado);
        Assert.Contains("aparece en su archivo", r.Mensaje);
    }

    [Fact]
    public async Task ArchivoTitulo_PdfDeOtroDocumento_GeneraAlerta()
    {
        var almacen = new AlmacenArchivosMemoria();
        var r = await Titularidad(ConArchivo(almacen, Pdf("N.º PPD-NAL-086678 a favor de otra persona"), "application/pdf"), almacen);
        Assert.Equal(EstadoCapa.Alerta, r.Estado);
        Assert.Contains("no contiene el número SPP-NAL-070167", r.Mensaje);
        Assert.Null(r.Prioridad);
    }

    [Fact]
    public async Task ArchivoTitulo_UnaImagenNoSePuedeLeer_AlertaBaja()
    {
        var almacen = new AlmacenArchivosMemoria();
        var r = await Titularidad(ConArchivo(almacen, [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3], "image/jpeg"), almacen);
        Assert.Equal(EstadoCapa.Alerta, r.Estado);
        Assert.Equal(PrioridadAlerta.Baja, r.Prioridad);
        Assert.Contains("no pudo leer", r.Mensaje);
    }
}
