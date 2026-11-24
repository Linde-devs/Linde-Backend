using LINDE.Api.Dominio;

namespace LINDE.Api.Contratos;

/// <summary>RF-01. Registro de un productor con su cuenta de acceso (registro público o hecho por un administrador).</summary>
public record RegistroProductorDto(string Nombre, string Apellido, string Ci, string? Runpa, string Telefono, string Municipio,
    string Correo, string Contrasena);

/// <summary>Edición de los datos del productor. La cuenta de acceso no se cambia aquí.</summary>
public record ProductorDto(string Nombre, string Apellido, string Ci, string? Runpa, string Telefono, string Municipio);

/// <summary>Lo que el frontend ve de un productor.</summary>
public record ProductorVista(int Id, string Codigo, string Nombre, string Apellido, string Ci, string? Runpa, string Telefono,
    string Municipio, string Correo, DateTime FechaRegistro, int CantidadPredios);

/// <summary>
/// RF-02. ProductorId solo lo usan los roles internos: si quien registra es un productor,
/// el predio queda a su nombre aunque envíe otro id.
/// </summary>
/// <summary>
/// Datos del predio. El título de propiedad del INRA se registra junto con el predio: con el número de título,
/// el CI del titular y la superficie titulada se crea (o actualiza) su documento.
/// </summary>
public record PredioDto(int? ProductorId, string Nombre, string Municipio, string Cultivo, double SuperficieDeclaradaHa,
    string? NumeroTituloInra, double? DesmonteDeclaradoHa, string? TitularCi = null, double? SuperficieTituladaHa = null, string? Departamento = null);

/// <summary>Lo que el frontend ve de un predio, con el nombre del productor y si ya tiene polígono.</summary>
public record PredioVista(int Id, int ProductorId, string Productor, string Nombre, string Municipio, string Cultivo,
    double SuperficieDeclaradaHa, string? NumeroTituloInra, double? DesmonteDeclaradoHa, EstadoExpediente Estado,
    DateTime FechaRegistro, bool TienePoligono, bool EsPunto, double? AreaPoligonoHa,
    string? TitularCi = null, double? SuperficieTituladaHa = null, string Departamento = "");

public record CoordenadaDto(double Lon, double Lat);

public record VerticesDto(List<CoordenadaDto> Vertices, string? Motivo);

public record EditarVerticeDto(double Lon, double Lat, string? Motivo);

public record DocumentoDto(TipoDocumento Tipo, string? Numero, string? TitularCi, double? SuperficieHa);


/// <summary>Vista del polígono con su área calculada, para el frontend.</summary>
public record PoligonoVista(int Id, int PredioId, int Version, bool Vigente, bool EsPunto, double? AreaHa,
    DateTime Fecha, string Autor, string Motivo, IReadOnlyList<Coordenada> Vertices);

/// <summary>Alerta con el nombre de su predio, para la lista del productor.</summary>
public record AlertaVista(int Id, int PredioId, string Predio, CapaValidacion Capa, PrioridadAlerta Prioridad, string Mensaje,
    DateTime Fecha, bool Resuelta);

/// <summary>CU-11. Una capa de referencia cargada por el administrador.</summary>
public record CapaVista(int Id, TipoCapa Tipo, string Nombre, string Fuente, string Archivo, int CantidadElementos,
    DateTime FechaCarga, string CargadaPor, bool Vigente);
