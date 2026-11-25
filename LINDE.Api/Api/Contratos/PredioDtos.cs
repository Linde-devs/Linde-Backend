using LINDE.Api.Dominio;

namespace LINDE.Api.Contratos;

/// <summary>
/// RF-02. Datos del predio. ProductorId solo lo usan los roles internos: si quien registra es un productor,
/// el predio queda a su nombre aunque envíe otro id. El título de propiedad del INRA se registra junto con el
/// predio: con el número de título, el CI del titular y la superficie titulada se crea (o actualiza) su documento.
/// </summary>
public record PredioDto(int? ProductorId, string Nombre, string Municipio, string Cultivo, double SuperficieDeclaradaHa,
    string? NumeroTituloInra, double? DesmonteDeclaradoHa, string? TitularCi = null, double? SuperficieTituladaHa = null, string? Departamento = null);

/// <summary>Lo que el frontend ve de un predio, con el nombre del productor y si ya tiene polígono.</summary>
public record PredioVista(int Id, int ProductorId, string Productor, string Nombre, string Municipio, string Cultivo,
    double SuperficieDeclaradaHa, string? NumeroTituloInra, double? DesmonteDeclaradoHa, EstadoExpediente Estado,
    DateTime FechaRegistro, bool TienePoligono, bool EsPunto, double? AreaPoligonoHa,
    string? TitularCi = null, double? SuperficieTituladaHa = null, string Departamento = "");
