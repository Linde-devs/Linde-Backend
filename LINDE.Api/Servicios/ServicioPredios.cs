using LINDE.Api.Auditoria;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Servicios;

/// <summary>CU-03 (gestionar predios) y CU-09 (resolver casos en revisión).</summary>
public interface IServicioPredios
{
    IReadOnlyList<Predio> Listar(int? productorId = null);
    Predio Obtener(int id);
    Predio Crear(PredioDto dto);
    Predio Actualizar(int id, PredioDto dto);
    void Eliminar(int id);
    Predio Aprobar(int id, string? comentario);
    Predio Observar(int id, string comentario);
}

public class ServicioPredios(
    IRepositorio<Predio> predios,
    IRepositorio<Productor> productores,
    IRepositorio<Poligono> poligonos,
    IRepositorio<Documento> documentos,
    IRepositorio<Alerta> alertas) : IServicioPredios
{
    public IReadOnlyList<Predio> Listar(int? productorId = null) =>
        predios.Listar(p => productorId is null || p.ProductorId == productorId);

    public Predio Obtener(int id) =>
        predios.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe el predio {id}.");

    public Predio Crear(PredioDto dto)
    {
        Validar(dto);
        return predios.Agregar(new Predio
        {
            ProductorId = dto.ProductorId,
            Nombre = dto.Nombre.Trim(),
            Municipio = dto.Municipio.Trim(),
            SuperficieDeclaradaHa = dto.SuperficieDeclaradaHa,
            DesmonteDeclaradoHa = dto.DesmonteDeclaradoHa
        });
    }

    public Predio Actualizar(int id, PredioDto dto)
    {
        var predio = Obtener(id);
        predio.VerificarEdicion();
        Validar(dto);

        predio.ProductorId = dto.ProductorId;
        predio.Nombre = dto.Nombre.Trim();
        predio.Municipio = dto.Municipio.Trim();
        predio.SuperficieDeclaradaHa = dto.SuperficieDeclaradaHa;
        predio.DesmonteDeclaradoHa = dto.DesmonteDeclaradoHa;
        predios.Actualizar(predio);
        return predio;
    }

    public void Eliminar(int id)
    {
        var predio = Obtener(id);
        predio.VerificarEdicion();

        foreach (var poligono in poligonos.Listar(p => p.PredioId == id)) poligonos.Eliminar(poligono.Id);
        foreach (var documento in documentos.Listar(d => d.PredioId == id)) documentos.Eliminar(documento.Id);
        predios.Eliminar(id);
    }

    public Predio Aprobar(int id, string? comentario)
    {
        var predio = Obtener(id);
        predio.Aprobar(comentario);
        predios.Actualizar(predio);
        CerrarAlertas(id);
        return predio;
    }

    public Predio Observar(int id, string comentario)
    {
        var predio = Obtener(id);
        predio.Observar(comentario);
        predios.Actualizar(predio);
        CerrarAlertas(id);
        return predio;
    }

    private void CerrarAlertas(int predioId)
    {
        foreach (var alerta in alertas.Listar(a => a.PredioId == predioId && !a.Resuelta))
        {
            alerta.Resuelta = true;
            alertas.Actualizar(alerta);
        }
    }

    private void Validar(PredioDto dto)
    {
        if (productores.Obtener(dto.ProductorId) is null)
            throw new DatosInvalidosException($"No existe el productor {dto.ProductorId}.");
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new DatosInvalidosException("El nombre del predio es obligatorio.");
        if (dto.SuperficieDeclaradaHa <= 0)
            throw new DatosInvalidosException("La superficie declarada debe ser mayor a cero.");
        if (dto.DesmonteDeclaradoHa is < 0)
            throw new DatosInvalidosException("El desmonte declarado no puede ser negativo.");
    }
}

/// <summary>
/// Patrón Decorator: envuelve al servicio real con la misma interfaz y registra cada operación
/// en la bitácora (usuario, fecha, valores anteriores y resultado). ServicioPredios no sabe que está siendo auditado.
/// </summary>
public class ServicioPrediosAuditado(IServicioPredios interno, AuditorOperaciones auditor) : IServicioPredios
{
    private const string Entidad = "Predio";

    public IReadOnlyList<Predio> Listar(int? productorId = null) => interno.Listar(productorId);

    public Predio Obtener(int id) => interno.Obtener(id);

    public Predio Crear(PredioDto dto) =>
        auditor.Ejecutar("Crear predio", Entidad, null, null, () => interno.Crear(dto), p => p.Id.ToString());

    public Predio Actualizar(int id, PredioDto dto) =>
        auditor.Ejecutar("Actualizar predio", Entidad, id.ToString(), Anterior(id), () => interno.Actualizar(id, dto));

    public void Eliminar(int id) =>
        auditor.Ejecutar("Eliminar predio", Entidad, id.ToString(), Anterior(id), () => interno.Eliminar(id));

    public Predio Aprobar(int id, string? comentario) =>
        auditor.Ejecutar("Aprobar predio", Entidad, id.ToString(), Anterior(id), () => interno.Aprobar(id, comentario));

    public Predio Observar(int id, string comentario) =>
        auditor.Ejecutar("Observar predio", Entidad, id.ToString(), Anterior(id), () => interno.Observar(id, comentario));

    private string? Anterior(int id)
    {
        try
        {
            return AuditorOperaciones.Instantanea(interno.Obtener(id));
        }
        catch (RecursoNoEncontradoException)
        {
            return null;
        }
    }
}
