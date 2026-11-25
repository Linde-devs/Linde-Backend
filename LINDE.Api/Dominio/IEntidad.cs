namespace LINDE.Api.Dominio;

/// <summary>Toda entidad guardada en un repositorio tiene un identificador numérico.</summary>
public interface IEntidad
{
    int Id { get; set; }
}
