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
        PredioAccesible(documento.PredioId).VerificarEdicion();
        documentos.Eliminar(id);
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
