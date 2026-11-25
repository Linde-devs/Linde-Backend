using LINDE.Api.Dominio;

namespace LINDE.Api.Contratos;

public record DocumentoDto(TipoDocumento Tipo, string? Numero, string? TitularCi, double? SuperficieHa);
