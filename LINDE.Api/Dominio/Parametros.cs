namespace LINDE.Api.Dominio;

/// <summary>Parámetros del sistema (CU-12). Ningún umbral queda fijo en el código.</summary>
public sealed record Parametros
{
    public DateOnly FechaCorte { get; init; } = new(2020, 12, 31);
    public double UmbralPoligonoObligatorioHa { get; init; } = 4;
    public double ToleranciaSuperposicionM2 { get; init; } = 100;
    public double ToleranciaSuperposicionPorcentaje { get; init; } = 0.1;

    /// <summary>ERS RF-06: la superposición bloquea el envío. Decisión pendiente; se puede cambiar sin tocar el código.</summary>
    public bool SuperposicionBloquea { get; init; } = true;

    public double Ley741DesmonteMaximoHa { get; init; } = 20;
    public double Ley741PredioMaximoHa { get; init; } = 50;
    public double PerdidaCoberturaAlertaPorcentaje { get; init; } = 10;
    public double BufferMetros { get; init; } = 128;
}
