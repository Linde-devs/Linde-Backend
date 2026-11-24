using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Servicios;

/// <summary>CU-05: documentos de respaldo (título del INRA, POP y Plan de Desmonte de la ABT).</summary>
public interface IServicioDocumentos
{
    IReadOnlyList<Documento> Listar(int predioId);
    Documento Agregar(int predioId, DocumentoDto dto);
    void Eliminar(int id);

    /// <summary>RF-16. Sube (o reemplaza) el archivo PDF o imagen del documento.</summary>
    Documento AdjuntarArchivo(int id, string nombreArchivo, Stream contenido);

    /// <summary>RF-16. El archivo del documento, para verlo o descargarlo.</summary>
    ArchivoDocumento AbrirArchivo(int id);
}

/// <summary>Archivo de un documento listo para enviar al navegador.</summary>
public record ArchivoDocumento(Stream Contenido, string Tipo, string Nombre);

public class ServicioDocumentos(
    IRepositorio<Predio> predios,
    IRepositorio<Documento> documentos,
    ControlAcceso acceso,
    IAlmacenArchivos? almacen = null) : IServicioDocumentos
{
    /// <summary>RF-16: tamaño máximo del archivo de un documento (10 MB, suficiente para un escaneo legible).</summary>
    public const long TamanoMaximoBytes = 10 * 1024 * 1024;

    private readonly IAlmacenArchivos _almacen = almacen ?? new AlmacenArchivosMemoria();

    public IReadOnlyList<Documento> Listar(int predioId)
    {
        PredioAccesible(predioId);
        return documentos.Listar(d => d.PredioId == predioId);
    }

    public Documento Agregar(int predioId, DocumentoDto dto)
    {
        var predio = PredioAccesible(predioId);
        predio.VerificarEdicion();

        // Todo documento necesita su número y una superficie mayor a cero: un registro vacío no respalda nada.
        // En el POP y el Plan de desmonte es la superficie autorizada por la ABT, que la capa de uso de suelo compara.
        var nombre = NombreDocumento(dto.Tipo);
        if (string.IsNullOrWhiteSpace(dto.Numero))
            throw new DatosInvalidosException($"Escribe el número del {nombre}.", "numero");
        if (dto.Numero.Trim().Length > 30)
            throw new DatosInvalidosException($"El número del {nombre} admite hasta 30 caracteres.", "numero");
        if (dto.SuperficieHa is null)
            throw new DatosInvalidosException(dto.Tipo == TipoDocumento.TituloPropiedad
                ? "Escribe la superficie titulada (ha)." : $"Escribe la superficie autorizada (ha) del {nombre}.", "superficieHa");
        if (dto.SuperficieHa <= 0)
            throw new DatosInvalidosException("La superficie del documento debe ser mayor a cero.", "superficieHa");

        // El título del INRA es uno solo por predio y su número es el del predio: se registra aquí o en el formulario
        // del predio, y los dos quedan iguales.
        if (dto.Tipo == TipoDocumento.TituloPropiedad)
        {
            if (documentos.Listar(d => d.PredioId == predioId && d.Tipo == TipoDocumento.TituloPropiedad).Any())
                throw new DatosInvalidosException("El predio ya tiene título de propiedad. Edítalo desde Gestionar predios.");
            predio.NumeroTituloInra = dto.Numero.Trim();
            predios.Actualizar(predio);
        }

        return documentos.Agregar(new Documento
        {
            PredioId = predioId,
            Tipo = dto.Tipo,
            Numero = dto.Numero?.Trim(),
            TitularCi = dto.TitularCi?.Trim(),
            SuperficieHa = dto.SuperficieHa
        });
    }

    private static string NombreDocumento(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.TituloPropiedad => "título de propiedad del INRA",
        TipoDocumento.PlanOrdenamientoPredial => "Plan de Ordenamiento Predial",
        TipoDocumento.PlanDesmonte => "Plan de Desmonte",
        _ => "documento"
    };

    public void Eliminar(int id)
    {
        var documento = documentos.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe el documento {id}.");
        var predio = PredioAccesible(documento.PredioId);
        predio.VerificarEdicion();
        documentos.Eliminar(id);
        if (documento.ArchivoClave is { } clave) _almacen.Eliminar(clave);
        if (documento.Tipo == TipoDocumento.TituloPropiedad)
        {
            predio.NumeroTituloInra = null;
            predios.Actualizar(predio);
        }
    }

    public Documento AdjuntarArchivo(int id, string nombreArchivo, Stream contenido)
    {
        var documento = documentos.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe el documento {id}.");
        PredioAccesible(documento.PredioId).VerificarEdicion();

        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        if (!TiposPermitidos.TryGetValue(extension, out var tipo))
            throw new DatosInvalidosException("Solo se aceptan archivos PDF, JPG o PNG.", "archivo");

        // Se copia a memoria para medirlo y revisar su firma antes de guardarlo.
        using var copia = new MemoryStream();
        contenido.CopyTo(copia);
        if (copia.Length == 0)
            throw new DatosInvalidosException("El archivo está vacío.", "archivo");
        if (copia.Length > TamanoMaximoBytes)
            throw new DatosInvalidosException($"El archivo pesa {copia.Length / 1024d / 1024d:0.#} MB; el máximo es {TamanoMaximoBytes / 1024 / 1024} MB.", "archivo");
        if (!FirmaCorrecta(copia.GetBuffer(), extension))
            throw new DatosInvalidosException($"El contenido del archivo no es un {extension.TrimStart('.').ToUpperInvariant()} válido.", "archivo");

        copia.Position = 0;
        var anterior = documento.ArchivoClave;
        documento.ArchivoClave = _almacen.Guardar(copia, extension);
        documento.ArchivoNombre = Path.GetFileName(nombreArchivo);
        documento.ArchivoTipo = tipo;
        documento.ArchivoBytes = copia.Length;
        documento.ArchivoFecha = DateTime.UtcNow;
        documentos.Actualizar(documento);

        // El archivo reemplazado se borra después de guardar el nuevo, para no quedarse sin ninguno si algo falla.
        if (anterior is not null) _almacen.Eliminar(anterior);
        return documento;
    }

    public ArchivoDocumento AbrirArchivo(int id)
    {
        var documento = documentos.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe el documento {id}.");
        PredioAccesible(documento.PredioId);
        if (documento.ArchivoClave is null)
            throw new RecursoNoEncontradoException("El documento todavía no tiene archivo.");
        return new ArchivoDocumento(_almacen.Abrir(documento.ArchivoClave), documento.ArchivoTipo ?? "application/octet-stream",
            documento.ArchivoNombre ?? $"documento-{id}");
    }

    private static readonly Dictionary<string, string> TiposPermitidos = new()
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    /// <summary>
    /// Revisa los primeros bytes: cambiar la extensión de un .exe a .pdf no alcanza para que se acepte.
    /// PDF empieza con "%PDF", JPG con FF D8 FF y PNG con 89 50 4E 47.
    /// </summary>
    private static bool FirmaCorrecta(byte[] datos, string extension) => extension switch
    {
        ".pdf" => datos.Length >= 4 && datos[0] == 0x25 && datos[1] == 0x50 && datos[2] == 0x44 && datos[3] == 0x46,
        ".jpg" or ".jpeg" => datos.Length >= 3 && datos[0] == 0xFF && datos[1] == 0xD8 && datos[2] == 0xFF,
        ".png" => datos.Length >= 4 && datos[0] == 0x89 && datos[1] == 0x50 && datos[2] == 0x4E && datos[3] == 0x47,
        _ => false
    };

    private Predio PredioAccesible(int predioId)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        acceso.VerificarPredio(predio);
        return predio;
    }
}

/// <summary>Patrón Decorator: auditoría del CU-05.</summary>
public class ServicioDocumentosAuditado(IServicioDocumentos interno, IRepositorio<Documento> documentos, AuditorOperaciones auditor) : IServicioDocumentos
{
    private const string Entidad = "Documento";

    public IReadOnlyList<Documento> Listar(int predioId) => interno.Listar(predioId);

    public Documento Agregar(int predioId, DocumentoDto dto) =>
        auditor.Ejecutar($"Agregar documento ({dto.Tipo})", Entidad, null, null,
            () => interno.Agregar(predioId, dto), d => d.Id.ToString());

    public void Eliminar(int id) =>
        auditor.Ejecutar("Eliminar documento", Entidad, id.ToString(),
            AuditorOperaciones.Instantanea(documentos.Obtener(id)), () => interno.Eliminar(id));

    public Documento AdjuntarArchivo(int id, string nombreArchivo, Stream contenido) =>
        auditor.Ejecutar($"Subir archivo ({Path.GetFileName(nombreArchivo)})", Entidad, id.ToString(),
            AuditorOperaciones.Instantanea(documentos.Obtener(id)), () => interno.AdjuntarArchivo(id, nombreArchivo, contenido));

    public ArchivoDocumento AbrirArchivo(int id) => interno.AbrirArchivo(id);
}
