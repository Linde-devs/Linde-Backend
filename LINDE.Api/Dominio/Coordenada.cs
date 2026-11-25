namespace LINDE.Api.Dominio;

/// <summary>Coordenada geográfica en grados (WGS84). Igual que en GeoJSON: longitud primero, latitud después.</summary>
public readonly record struct Coordenada(double Lon, double Lat);
