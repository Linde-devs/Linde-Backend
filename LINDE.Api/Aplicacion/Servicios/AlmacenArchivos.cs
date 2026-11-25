namespace LINDE.Api.Servicios;

/// <summary>
/// RF-16. Dónde se guardan los archivos de los documentos. El servicio solo conoce esta interfaz: hoy es una carpeta
/// del servidor y, si se despliega en la nube, se cambia por un almacenamiento de objetos sin tocar el servicio.
/// </summary>
public interface IAlmacenArchivos
{
    /// <summary>Guarda el contenido y devuelve la clave con la que se recupera.</summary>
    string Guardar(Stream contenido, string extension);

    Stream Abrir(string clave);

    void Eliminar(string clave);
}

/// <summary>
/// Guarda los archivos en una carpeta del servidor ("Archivos:Carpeta", por defecto "archivos" junto a la API).
/// El nombre es un identificador aleatorio: nunca se usa el nombre que envió el usuario para escribir en disco.
/// La carpeta está en el .gitignore: los documentos no se suben al repositorio.
/// </summary>
public class AlmacenArchivosLocal : IAlmacenArchivos
{
    private readonly string _carpeta;

    public AlmacenArchivosLocal(IConfiguration configuracion, IWebHostEnvironment entorno)
    {
        var carpeta = configuracion["Archivos:Carpeta"] ?? "archivos";
        _carpeta = Path.GetFullPath(Path.IsPathRooted(carpeta) ? carpeta : Path.Combine(entorno.ContentRootPath, carpeta));
        Directory.CreateDirectory(_carpeta);
    }

    public string Guardar(Stream contenido, string extension)
    {
        var clave = $"{Guid.NewGuid():N}{extension}";
        using var destino = File.Create(Ruta(clave));
        contenido.CopyTo(destino);
        return clave;
    }

    public Stream Abrir(string clave)
    {
        var ruta = Ruta(clave);
        if (!File.Exists(ruta))
            throw new Dominio.RecursoNoEncontradoException("El archivo del documento ya no está en el servidor.");
        return File.OpenRead(ruta);
    }

    public void Eliminar(string clave)
    {
        var ruta = Ruta(clave);
        if (File.Exists(ruta)) File.Delete(ruta);
    }

    /// <summary>La clave la genera el sistema, pero igual se comprueba que no salga de la carpeta.</summary>
    private string Ruta(string clave)
    {
        var ruta = Path.GetFullPath(Path.Combine(_carpeta, Path.GetFileName(clave)));
        if (!ruta.StartsWith(_carpeta, StringComparison.Ordinal))
            throw new Dominio.DatosInvalidosException("Clave de archivo inválida.");
        return ruta;
    }
}

/// <summary>Archivos en memoria, para las pruebas y para "Datos:Proveedor" = "Memoria".</summary>
public class AlmacenArchivosMemoria : IAlmacenArchivos
{
    private readonly Dictionary<string, byte[]> _archivos = new();

    public IReadOnlyCollection<string> Claves => _archivos.Keys;

    public string Guardar(Stream contenido, string extension)
    {
        var clave = $"{Guid.NewGuid():N}{extension}";
        using var copia = new MemoryStream();
        contenido.CopyTo(copia);
        lock (_archivos) _archivos[clave] = copia.ToArray();
        return clave;
    }

    public Stream Abrir(string clave)
    {
        lock (_archivos)
            return _archivos.TryGetValue(clave, out var datos)
                ? new MemoryStream(datos)
                : throw new Dominio.RecursoNoEncontradoException("El archivo del documento ya no está en el servidor.");
    }

    public void Eliminar(string clave)
    {
        lock (_archivos) _archivos.Remove(clave);
    }
}
