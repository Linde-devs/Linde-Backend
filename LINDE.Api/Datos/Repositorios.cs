using System.Linq.Expressions;
using LINDE.Api.Dominio;

namespace LINDE.Api.Datos;

/// <summary>
/// Acceso a datos. Hay dos implementaciones con la misma interfaz: RepositorioEf (PostgreSQL + PostGIS)
/// y RepositorioMemoria (listas, para las pruebas). Los servicios no saben cuál reciben.
/// El filtro es una expresión y no una función: así EF Core lo traduce a SQL y la base filtra,
/// en lugar de traer la tabla completa a memoria.
/// </summary>
public interface IRepositorio<T> where T : class, IEntidad
{
    T? Obtener(int id);
    IReadOnlyList<T> Listar(Expression<Func<T, bool>>? filtro = null);
    T Agregar(T entidad);
    void Actualizar(T entidad);
    bool Eliminar(int id);
}

/// <summary>Datos en listas en memoria. Lo usan las pruebas automáticas y "Datos:Proveedor" = "Memoria".</summary>
public class RepositorioMemoria<T> : IRepositorio<T> where T : class, IEntidad
{
    private readonly List<T> _datos = new();
    private readonly object _candado = new();
    private int _siguienteId = 1;

    public T? Obtener(int id)
    {
        lock (_candado) return _datos.FirstOrDefault(e => e.Id == id);
    }

    public IReadOnlyList<T> Listar(Expression<Func<T, bool>>? filtro = null)
    {
        var condicion = filtro?.Compile();
        lock (_candado) return (condicion is null ? _datos : _datos.Where(condicion)).ToList();
    }

    public T Agregar(T entidad)
    {
        lock (_candado)
        {
            entidad.Id = _siguienteId++;
            _datos.Add(entidad);
            return entidad;
        }
    }

    public void Actualizar(T entidad)
    {
        lock (_candado)
        {
            var indice = _datos.FindIndex(e => e.Id == entidad.Id);
            if (indice < 0) throw new RecursoNoEncontradoException($"No existe el registro {entidad.Id}.");
            _datos[indice] = entidad;
        }
    }

    public bool Eliminar(int id)
    {
        lock (_candado) return _datos.RemoveAll(e => e.Id == id) > 0;
    }
}

/// <summary>Los parámetros son un solo registro, no una lista.</summary>
public interface IRepositorioParametros
{
    Parametros Obtener();
    void Guardar(Parametros parametros);
}

public class RepositorioParametrosMemoria : IRepositorioParametros
{
    private Parametros _parametros = new();

    public Parametros Obtener() => _parametros;

    public void Guardar(Parametros parametros) => _parametros = parametros;
}
