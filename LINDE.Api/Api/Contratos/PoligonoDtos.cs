using LINDE.Api.Dominio;

namespace LINDE.Api.Contratos;

/// <summary>RF-04: PrecisionM es la precisión del GPS en metros; null si el vértice no viene del GPS.</summary>
public record CoordenadaDto(double Lon, double Lat, double? PrecisionM = null);

public record VerticesDto(List<CoordenadaDto> Vertices, string? Motivo);

public record EditarVerticeDto(double Lon, double Lat, string? Motivo);

/// <summary>Vista del polígono con su área calculada, para el frontend.</summary>
public record PoligonoVista(int Id, int PredioId, int Version, bool Vigente, bool EsPunto, double? AreaHa,
    DateTime Fecha, string Autor, string Motivo, IReadOnlyList<Coordenada> Vertices, IReadOnlyList<double?> PrecisionesM);
