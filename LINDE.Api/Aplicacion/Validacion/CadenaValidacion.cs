using LINDE.Api.Dominio;

namespace LINDE.Api.Validacion;

public class ContextoValidacion
{
    public required Predio Predio { get; init; }
    public required Poligono Poligono { get; init; }
    public Productor? Productor { get; init; }
    public IReadOnlyList<Documento> Documentos { get; init; } = Array.Empty<Documento>();
    public required Parametros Parametros { get; init; }
    public CancellationToken Cancelacion { get; init; }
    
    public double AreaPoligonoHa { get; set; }

    public List<ResultadoCapa> Resultados { get; } = new();
    public bool Cortado { get; set; }
}

public abstract class ValidadorCapa
{
    private ValidadorCapa? _siguiente;

    public abstract CapaValidacion Capa { get; }
    
    public abstract int Orden { get; }
    
    protected virtual bool PuedeCortar => false;

    public ValidadorCapa EstablecerSiguiente(ValidadorCapa siguiente)
    {
        _siguiente = siguiente;
        return siguiente;
    }

    public async Task ProcesarAsync(ContextoValidacion contexto)
    {
        var resultado = await ValidarAsync(contexto);
        contexto.Resultados.Add(resultado);

        if (PuedeCortar && resultado.Estado == EstadoCapa.Rechazado)
        {
            contexto.Cortado = true;
            return;
        }

        if (_siguiente is not null)
            await _siguiente.ProcesarAsync(contexto);
    }

    protected abstract Task<ResultadoCapa> ValidarAsync(ContextoValidacion contexto);

    protected ResultadoCapa Conforme(string mensaje) => new() { Capa = Capa, Estado = EstadoCapa.Conforme, Mensaje = mensaje };
    protected ResultadoCapa ConAlerta(string mensaje, PrioridadAlerta? prioridad = null) =>
        new() { Capa = Capa, Estado = EstadoCapa.Alerta, Mensaje = mensaje, Prioridad = prioridad };
    protected ResultadoCapa Rechazado(string mensaje) => new() { Capa = Capa, Estado = EstadoCapa.Rechazado, Mensaje = mensaje };

    /// <summary>Ordena los validadores que entrega el contenedor y los enlaza. Devuelve el primer eslabón.</summary>
    public static ValidadorCapa ArmarCadena(IEnumerable<ValidadorCapa> validadores)
    {
        var ordenados = validadores.OrderBy(v => v.Orden).ToList();
        if (ordenados.Count == 0)
            throw new InvalidOperationException("No hay validadores registrados.");

        for (var i = 0; i < ordenados.Count - 1; i++)
            ordenados[i].EstablecerSiguiente(ordenados[i + 1]);

        return ordenados[0];
    }
}
