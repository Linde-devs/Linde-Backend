using LINDE.Api.Dominio;

namespace LINDE.Api.Contratos;

public record ProductorDto(string Nombre, string Apellido, string Ci);

public record PredioDto(int ProductorId, string Nombre, string Municipio, double SuperficieDeclaradaHa, double? DesmonteDeclaradoHa);

public record CoordenadaDto(double Lon, double Lat);

public record VerticesDto(List<CoordenadaDto> Vertices, string? Motivo);

public record EditarVerticeDto(double Lon, double Lat, string? Motivo);

public record DocumentoDto(TipoDocumento Tipo, string? Numero, string? TitularCi, double? SuperficieHa);

public record ResolucionDto(string? Comentario);

/// <summary>Vista del polígono con su área calculada, para el frontend.</summary>
public record PoligonoVista(int Id, int PredioId, int Version, bool Vigente, bool EsPunto, double? AreaHa,
    DateTime Fecha, string Autor, string Motivo, IReadOnlyList<Coordenada> Vertices);
