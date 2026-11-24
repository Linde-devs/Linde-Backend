using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Servicios;

/// <summary>CU-03. Gestionar predios. El estado lo cambia solo el envío a revisión: nadie aprueba ni observa dentro del sistema.</summary>
public interface IServicioPredios
{
    IReadOnlyList<Predio> Listar(int? productorId = null);
    Predio Obtener(int id);
    Predio Crear(PredioDto dto);
    Predio Actualizar(int id, PredioDto dto);
    void Eliminar(int id);
}

public class ServicioPredios(
    IRepositorio<Predio> predios,
    IRepositorio<Productor> productores,
    IRepositorio<Poligono> poligonos,
    IRepositorio<Documento> documentos,
    ControlAcceso acceso) : IServicioPredios
{
    /// <summary>Un productor solo recibe sus predios, aunque pida los de otro (RNF-03).</summary>
    public IReadOnlyList<Predio> Listar(int? productorId = null)
    {
        var filtro = acceso.ProductorPropio ?? productorId;
        return predios.Listar(p => filtro == null || p.ProductorId == filtro)
            .OrderByDescending(p => p.FechaRegistro)
            .ToList();
    }

    public Predio Obtener(int id)
    {
        var predio = predios.Obtener(id) ?? throw new RecursoNoEncontradoException($"No existe el predio {id}.");
        acceso.VerificarPredio(predio);
        return predio;
    }

    public Predio Crear(PredioDto dto)
    {
        // Si registra un productor, el predio queda a su nombre; un rol interno debe indicar el productor.
        var productorId = acceso.ProductorPropio ?? dto.ProductorId
            ?? throw new DatosInvalidosException("Selecciona el productor del predio.", "productorId");
        var (departamento, municipio) = Validar(dto, productorId);

        var predio = predios.Agregar(new Predio
        {
            ProductorId = productorId,
            Nombre = dto.Nombre.Trim(),
            Departamento = departamento,
            Municipio = municipio,
            Cultivo = dto.Cultivo.Trim(),
            SuperficieDeclaradaHa = dto.SuperficieDeclaradaHa,
            NumeroTituloInra = Opcional(dto.NumeroTituloInra),
            DesmonteDeclaradoHa = dto.DesmonteDeclaradoHa
        });
        SincronizarTitulo(predio, dto);
        return predio;
    }

    /// <summary>
    /// El título del INRA se registra con el predio: si hay número de título, su documento se crea o se actualiza
    /// con el CI del titular y la superficie titulada. Así el título no queda partido entre dos pantallas.
    /// </summary>
    private void SincronizarTitulo(Predio predio, PredioDto dto)
    {
        if (predio.NumeroTituloInra is null)
            return;

        var titulo = documentos.Listar(d => d.PredioId == predio.Id && d.Tipo == TipoDocumento.TituloPropiedad).FirstOrDefault();
        if (titulo is null)
        {
            documentos.Agregar(new Documento
            {
                PredioId = predio.Id,
                Tipo = TipoDocumento.TituloPropiedad,
                Numero = predio.NumeroTituloInra,
                TitularCi = Opcional(dto.TitularCi),
                SuperficieHa = dto.SuperficieTituladaHa
            });
            return;
        }

        titulo.Numero = predio.NumeroTituloInra;
        if (dto.TitularCi is not null) titulo.TitularCi = Opcional(dto.TitularCi);
        if (dto.SuperficieTituladaHa is not null) titulo.SuperficieHa = dto.SuperficieTituladaHa;
        documentos.Actualizar(titulo);
    }

    public Predio Actualizar(int id, PredioDto dto)
    {
        var predio = Obtener(id);
        predio.VerificarEdicion();

        var productorId = acceso.ProductorPropio ?? dto.ProductorId ?? predio.ProductorId;
        var (departamento, municipio) = Validar(dto, productorId);

        predio.ProductorId = productorId;
        predio.Nombre = dto.Nombre.Trim();
        predio.Departamento = departamento;
        predio.Municipio = municipio;
        predio.Cultivo = dto.Cultivo.Trim();
        predio.SuperficieDeclaradaHa = dto.SuperficieDeclaradaHa;
        predio.NumeroTituloInra = Opcional(dto.NumeroTituloInra);
        predio.DesmonteDeclaradoHa = dto.DesmonteDeclaradoHa;
        predios.Actualizar(predio);
        SincronizarTitulo(predio, dto);
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

    /// <summary>RF-02: un predio sin productor asociado o sin superficie declarada no se guarda. La superficie acepta 2 decimales.</summary>
    private (string Departamento, string Municipio) Validar(PredioDto dto, int productorId)
    {
        if (productores.Obtener(productorId) is null)
            throw new DatosInvalidosException($"No existe el productor {productorId}.", "productorId");
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new DatosInvalidosException("El nombre del predio es obligatorio.", "nombre");
        if (string.IsNullOrWhiteSpace(dto.Departamento))
            throw new DatosInvalidosException("Selecciona el departamento.", "departamento");
        var departamento = DivisionPolitica.Departamento(dto.Departamento)
            ?? throw new DatosInvalidosException($"\"{dto.Departamento.Trim()}\" no es un departamento de Bolivia.", "departamento");
        if (string.IsNullOrWhiteSpace(dto.Municipio))
            throw new DatosInvalidosException("El municipio es obligatorio.", "municipio");
        var municipio = DivisionPolitica.Municipio(departamento, dto.Municipio)
            ?? throw new DatosInvalidosException(
                $"\"{dto.Municipio.Trim()}\" no es un municipio de {departamento}. Elige uno de la lista.", "municipio");
        if (string.IsNullOrWhiteSpace(dto.Cultivo))
            throw new DatosInvalidosException("El cultivo es obligatorio.", "cultivo");
        if (dto.SuperficieDeclaradaHa <= 0)
            throw new DatosInvalidosException("La superficie declarada debe ser mayor a cero.", "superficieDeclaradaHa");
        if (!DosDecimales(dto.SuperficieDeclaradaHa))
            throw new DatosInvalidosException("La superficie acepta como máximo 2 decimales.", "superficieDeclaradaHa");
        if (dto.NumeroTituloInra?.Trim().Length > 30)
            throw new DatosInvalidosException("El número de título del INRA admite hasta 30 caracteres.", "numeroTituloInra");
        if (dto.DesmonteDeclaradoHa is < 0)
            throw new DatosInvalidosException("El desmonte declarado no puede ser negativo.", "desmonteDeclaradoHa");
        if (dto.DesmonteDeclaradoHa is double desmonte && !DosDecimales(desmonte))
            throw new DatosInvalidosException("El desmonte acepta como máximo 2 decimales.", "desmonteDeclaradoHa");
        if (dto.DesmonteDeclaradoHa > dto.SuperficieDeclaradaHa)
            throw new DatosInvalidosException("El desmonte no puede ser mayor que la superficie del predio.", "desmonteDeclaradoHa");
        if (!string.IsNullOrWhiteSpace(dto.TitularCi) && !System.Text.RegularExpressions.Regex.IsMatch(dto.TitularCi.Trim(), @"^\d{5,10}$"))
            throw new DatosInvalidosException("El CI del titular debe tener de 5 a 10 dígitos.", "titularCi");
        if (dto.SuperficieTituladaHa is <= 0)
            throw new DatosInvalidosException("La superficie titulada debe ser mayor a cero.", "superficieTituladaHa");
        // El título es opcional al registrar el predio. Un CI sin número de título se ignora (no hay documento al que
        // asociarlo), pero una superficie titulada sin número indica que falta el número.
        if (dto.SuperficieTituladaHa is not null && string.IsNullOrWhiteSpace(dto.NumeroTituloInra))
            throw new DatosInvalidosException("Escribe el número de título INRA.", "numeroTituloInra");
        return (departamento, municipio);
    }

    private static bool DosDecimales(double valor) => Math.Abs(valor * 100 - Math.Round(valor * 100)) < 1e-6;

    private static string? Opcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
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
