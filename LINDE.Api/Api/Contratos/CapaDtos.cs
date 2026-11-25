using LINDE.Api.Dominio;

namespace LINDE.Api.Contratos;

/// <summary>CU-11. Una capa de referencia cargada por el administrador.</summary>
public record CapaVista(int Id, TipoCapa Tipo, string Nombre, string Fuente, string Archivo, int CantidadElementos,
    DateTime FechaCarga, string CargadaPor, bool Vigente);
