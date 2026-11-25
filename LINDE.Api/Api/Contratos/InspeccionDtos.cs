using LINDE.Api.Dominio;

namespace LINDE.Api.Contratos;

/// <summary>RF-14. Datos de la inspección de campo; los nulos se revisan en el servicio para dar un mensaje claro.</summary>
public record InspeccionDto(DateOnly? Fecha, string? Tecnico, ResultadoInspeccion? Resultado, string? Comentario);
