using LINDE.Api.Dominio;

namespace LINDE.Api.Contratos;

/// <summary>Alerta con el nombre de su predio, para la lista del productor.</summary>
public record AlertaVista(int Id, int PredioId, string Predio, CapaValidacion Capa, PrioridadAlerta Prioridad, string Mensaje,
    DateTime Fecha, bool Resuelta);
