using System.Text.RegularExpressions;
using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Servicios;

/// <summary>
/// CU-02 y RF-01. Registro de productores. Cada productor tiene su cuenta de acceso con rol Productor,
/// que se crea en la misma operación. Según los documentos de patrones, aquí no se aplica ningún patrón
/// (una sola forma de crear la entidad y sin estados ni algoritmos alternativos).
/// </summary>
public partial class ServicioProductores(
    IRepositorio<Productor> productores,
    IRepositorio<Predio> predios,
    IRepositorio<Usuario> usuarios,
    ServicioAutenticacion cuentas)
{
    public const string MensajeDuplicado = "Productor ya registrado";

    public IReadOnlyList<ProductorVista> Listar() =>
        productores.Listar().OrderBy(p => p.Apellido).ThenBy(p => p.Nombre).Select(Vista).ToList();

    public ProductorVista Obtener(int id) => Vista(Buscar(id));

    /// <summary>
    /// Registra el productor y su cuenta. Primero valida todo; si algo falla no se guarda nada.
    /// Con datos completos se asigna un código único (PRD-000001).
    /// </summary>
    public ProductorVista Registrar(RegistroProductorDto dto)
    {
        var datos = Normalizar(new ProductorDto(dto.Nombre, dto.Apellido, dto.Ci, dto.Runpa, dto.Telefono, dto.Municipio));
        Validar(datos, null);

        var usuario = cuentas.CrearCuenta(datos.Nombre, datos.Apellido, datos.Ci, dto.Correo, dto.Contrasena, RolUsuario.Productor);

        var productor = productores.Agregar(new Productor
        {
            Nombre = datos.Nombre,
            Apellido = datos.Apellido,
            Ci = datos.Ci,
            Runpa = datos.Runpa,
            Telefono = datos.Telefono,
            Municipio = datos.Municipio,
            UsuarioId = usuario.Id
        });

        productor.Codigo = $"PRD-{productor.Id:D6}";
        productores.Actualizar(productor);

        usuario.ProductorId = productor.Id;
        usuarios.Actualizar(usuario);

        return Vista(productor);
    }

    public ProductorVista Actualizar(int id, ProductorDto dto)
    {
        var productor = Buscar(id);
        var datos = Normalizar(dto);
        Validar(datos, id);

        productor.Nombre = datos.Nombre;
        productor.Apellido = datos.Apellido;
        productor.Ci = datos.Ci;
        productor.Runpa = datos.Runpa;
        productor.Telefono = datos.Telefono;
        productor.Municipio = datos.Municipio;
        productores.Actualizar(productor);

        // La cuenta muestra el mismo nombre que el productor.
        if (usuarios.Obtener(productor.UsuarioId) is { } usuario)
        {
            usuario.Nombre = datos.Nombre;
            usuario.Apellido = datos.Apellido;
            usuario.Ci = datos.Ci;
            usuarios.Actualizar(usuario);
        }

        return Vista(productor);
    }

    /// <summary>Elimina el productor y su cuenta. No se permite si todavía tiene predios.</summary>
    public void Eliminar(int id)
    {
        var productor = Buscar(id);
        if (predios.Listar(p => p.ProductorId == id).Count > 0)
            throw new OperacionNoPermitidaException("No se puede eliminar un productor que tiene predios registrados.");

        productores.Eliminar(id);
        usuarios.Eliminar(productor.UsuarioId);
    }

    private Productor Buscar(int id) =>
        productores.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe el productor {id}.");

    private ProductorVista Vista(Productor p) => new(
        p.Id, p.Codigo, p.Nombre, p.Apellido, p.Ci, p.Runpa, p.Telefono, p.Municipio,
        usuarios.Obtener(p.UsuarioId)?.Correo ?? "",
        p.FechaRegistro,
        predios.Listar(x => x.ProductorId == p.Id).Count);

    private static ProductorDto Normalizar(ProductorDto dto) => new(
        dto.Nombre?.Trim() ?? "",
        dto.Apellido?.Trim() ?? "",
        (dto.Ci?.Trim() ?? "").ToUpperInvariant(),
        string.IsNullOrWhiteSpace(dto.Runpa) ? null : dto.Runpa.Trim().ToUpperInvariant(),
        (dto.Telefono ?? "").Replace(" ", "").Trim(),
        dto.Municipio?.Trim() ?? "");

    private void Validar(ProductorDto d, int? idActual)
    {
        if (d.Nombre.Length == 0) throw new DatosInvalidosException("Ingresa el nombre.", "nombre");
        if (d.Apellido.Length == 0) throw new DatosInvalidosException("Ingresa el apellido.", "apellido");
        if (!FormatoCiNit().IsMatch(d.Ci))
            throw new DatosInvalidosException("El CI o NIT debe tener entre 5 y 12 dígitos (con complemento opcional, por ejemplo 7845123-1A).", "ci");
        if (d.Runpa is not null && !FormatoRunpa().IsMatch(d.Runpa))
            throw new DatosInvalidosException("El código RUNPA solo admite letras, números y guiones (hasta 20 caracteres).", "runpa");
        if (!FormatoTelefono().IsMatch(d.Telefono))
            throw new DatosInvalidosException("El teléfono debe tener entre 7 y 12 dígitos.", "telefono");
        if (d.Municipio.Length == 0) throw new DatosInvalidosException("Ingresa el municipio.", "municipio");

        // RF-01: un CI o NIT repetido se rechaza con este mensaje exacto.
        if (productores.Listar(p => p.Ci == d.Ci && p.Id != idActual).Count > 0)
            throw new DatosInvalidosException(MensajeDuplicado, "ci");
    }

    [GeneratedRegex(@"^\d{5,12}(-[0-9A-Z]{1,2})?$")]
    private static partial Regex FormatoCiNit();

    [GeneratedRegex(@"^[0-9A-Z-]{1,20}$")]
    private static partial Regex FormatoRunpa();

    [GeneratedRegex(@"^\+?\d{7,12}$")]
    private static partial Regex FormatoTelefono();
}
