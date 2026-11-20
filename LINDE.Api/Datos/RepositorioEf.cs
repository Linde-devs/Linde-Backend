using System.Linq.Expressions;
using System.Text.Json;
using LINDE.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace LINDE.Api.Datos;

/// <summary>
/// Repositorio con EF Core sobre PostgreSQL + PostGIS. Cada operación que modifica datos se guarda de inmediato,
/// igual que hacía el repositorio en memoria, para que los servicios no cambien.
/// </summary>
public class RepositorioEf<T>(LindeDbContext db) : IRepositorio<T> where T : class, IEntidad
{
    private DbSet<T> Tabla => db.Set<T>();

    public T? Obtener(int id) => Tabla.Find(id);

    public IReadOnlyList<T> Listar(Expression<Func<T, bool>>? filtro = null) =>
        (filtro is null ? Tabla : Tabla.Where(filtro)).ToList();

    public T Agregar(T entidad)
    {
        Tabla.Add(entidad);
        db.SaveChanges();
        return entidad;
    }

    public void Actualizar(T entidad)
    {
        // Si la entidad vino de este mismo contexto, EF ya ve los cambios; si no, se adjunta como modificada.
        if (db.Entry(entidad).State == EntityState.Detached) Tabla.Update(entidad);
        db.SaveChanges();
    }

    public bool Eliminar(int id)
    {
        var entidad = Tabla.Find(id);
        if (entidad is null) return false;
        Tabla.Remove(entidad);
        db.SaveChanges();
        return true;
    }
}

/// <summary>Los parámetros (CU-12) son una sola fila con los valores en JSON.</summary>
public class RepositorioParametrosEf(LindeDbContext db) : IRepositorioParametros
{
    private const int IdUnico = 1;

    public Parametros Obtener()
    {
        var fila = db.Parametros.AsNoTracking().FirstOrDefault(f => f.Id == IdUnico);
        return fila is null ? new Parametros() : JsonSerializer.Deserialize<Parametros>(fila.Datos, Json.Opciones) ?? new Parametros();
    }

    public void Guardar(Parametros parametros)
    {
        var fila = db.Parametros.Find(IdUnico);
        var datos = JsonSerializer.Serialize(parametros, Json.Opciones);
        if (fila is null) db.Parametros.Add(new FilaParametros { Id = IdUnico, Datos = datos });
        else fila.Datos = datos;
        db.SaveChanges();
    }
}

/// <summary>Fila de la tabla Parametros.</summary>
public class FilaParametros
{
    public int Id { get; set; }
    public string Datos { get; set; } = "{}";
}
