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
}

public class ServicioDocumentos(IRepositorio<Predio> predios, IRepositorio<Documento> documentos, ControlAcceso acceso) : IServicioDocumentos
{
    public IReadOnlyList<Documento> Listar(int predioId)
    {
        PredioAccesible(predioId);
        return documentos.Listar(d => d.PredioId == predioId);
    }

    public Documento Agregar(int predioId, DocumentoDto dto)
    {
        var predio = PredioAccesible(predioId);
        predio.VerificarEdicion();

        if (dto.SuperficieHa is <= 0)
            throw new DatosInvalidosException("La superficie del documento debe ser mayor a cero.");

        // El título del INRA es uno solo por predio y su número es el del predio: se registra aquí o en el formulario
        // del predio, y los dos quedan iguales.
        if (dto.Tipo == TipoDocumento.TituloPropiedad)
        {
            if (string.IsNullOrWhiteSpace(dto.Numero))
                throw new DatosInvalidosException("Escribe el número de título INRA.", "numero");
            if (dto.Numero.Trim().Length > 30)
                throw new DatosInvalidosException("El número de título del INRA admite hasta 30 caracteres.", "numero");
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

    public void Eliminar(int id)
    {
        var documento = documentos.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe el documento {id}.");
        var predio = PredioAccesible(documento.PredioId);
        predio.VerificarEdicion();
        documentos.Eliminar(id);
        if (documento.Tipo == TipoDocumento.TituloPropiedad)
        {
            predio.NumeroTituloInra = null;
            predios.Actualizar(predio);
        }
    }

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
}
