using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Servicios;

/// <summary>
/// CU-02. CRUD simple: según los documentos de patrones, aquí no se aplica ningún patrón
/// (una sola forma de crear la entidad y sin estados ni algoritmos alternativos).
/// </summary>
public class ServicioProductores(IRepositorio<Productor> productores, IRepositorio<Predio> predios)
{
    public IReadOnlyList<Productor> Listar() => productores.Listar();

    public Productor Obtener(int id) =>
        productores.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe el productor {id}.");

    public Productor Crear(ProductorDto dto)
    {
        Validar(dto, null);
        return productores.Agregar(new Productor { Nombre = dto.Nombre.Trim(), Apellido = dto.Apellido.Trim(), Ci = dto.Ci.Trim() });
    }

    public Productor Actualizar(int id, ProductorDto dto)
    {
        var productor = Obtener(id);
        Validar(dto, id);
        productor.Nombre = dto.Nombre.Trim();
        productor.Apellido = dto.Apellido.Trim();
        productor.Ci = dto.Ci.Trim();
        productores.Actualizar(productor);
        return productor;
    }

    public void Eliminar(int id)
    {
        Obtener(id);
        if (predios.Listar(p => p.ProductorId == id).Count > 0)
            throw new OperacionNoPermitidaException("No se puede eliminar un productor que tiene predios registrados.");
        productores.Eliminar(id);
    }

    private void Validar(ProductorDto dto, int? idActual)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Apellido) || string.IsNullOrWhiteSpace(dto.Ci))
            throw new DatosInvalidosException("Nombre, apellido y CI son obligatorios.");

        if (productores.Listar(p => p.Ci == dto.Ci.Trim() && p.Id != idActual).Count > 0)
            throw new DatosInvalidosException("Ya existe un productor con ese CI.");
    }
}
