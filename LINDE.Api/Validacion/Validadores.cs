using LINDE.Api.Cobertura;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using LINDE.Api.Validacion.UsoSuelo;
using NetTopologySuite.Operation.Valid;

namespace LINDE.Api.Validacion;

/// <summary>
/// Capa 1. ¿El polígono es válido y único? Cerrado, sin lados cruzados, sin vértices duplicados,
/// obligatorio para predios de más de 4 ha, y sin superposición con otros predios.
/// Es la única capa que puede cortar la cadena.
/// </summary>
public class ValidadorGeometrico(IRepositorio<Poligono> poligonos) : ValidadorCapa
{
    public override CapaValidacion Capa => CapaValidacion.Geometrica;
    public override int Orden => 1;
    protected override bool PuedeCortar => true;

    protected override Task<ResultadoCapa> ValidarAsync(ContextoValidacion ctx) => Task.FromResult(Validar(ctx));

    private ResultadoCapa Validar(ContextoValidacion ctx)
    {
        var v = ctx.Poligono.Vertices;
        var p = ctx.Parametros;

        if (v.Count == 0)
            return Rechazado("El predio no tiene polígono ni punto registrado.");

        if (ctx.Poligono.EsPunto)
        {
            if (ctx.Predio.SuperficieDeclaradaHa > p.UmbralPoligonoObligatorioHa)
                return Rechazado($"El predio declara {ctx.Predio.SuperficieDeclaradaHa:0.##} ha. Con más de {p.UmbralPoligonoObligatorioHa:0.##} ha se requiere un polígono, no un punto.");

            ctx.AreaPoligonoHa = ctx.Predio.SuperficieDeclaradaHa;
            return Conforme($"Se registró un punto, suficiente para predios de hasta {p.UmbralPoligonoObligatorioHa:0.##} ha.");
        }

        if (v.Count < 4)
            return Rechazado("El polígono necesita al menos 3 vértices distintos.");

        if (v[0] != v[^1])
            return Rechazado("El polígono no está cerrado: el último vértice debe repetir el primero.");

        var sinCierre = v.Take(v.Count - 1).ToList();
        if (sinCierre.Distinct().Count() != sinCierre.Count)
            return Rechazado("El polígono tiene vértices duplicados.");

        var validacion = new IsValidOp(Geometrias.Crear(v));
        if (!validacion.IsValid)
        {
            var error = validacion.ValidationError;
            return Rechazado(error.ErrorType is TopologyValidationErrors.SelfIntersection or TopologyValidationErrors.RingSelfIntersection
                ? "Los lados del polígono se cruzan entre sí."
                : $"La geometría no es válida ({error.Message}).");
        }

        var centro = Geometrias.Centro(v);
        var propio = Geometrias.CrearProyectado(v, centro);
        ctx.AreaPoligonoHa = propio.Area / 10_000d;

        var peor = BuscarMayorSuperposicion(ctx, propio, centro);
        if (peor is { } s)
        {
            var porcentaje = s.AreaM2 / propio.Area * 100;
            var detalle = $"Se superpone {s.AreaM2:0} m² ({porcentaje:0.##} %) con el predio {s.PredioId}.";

            if (s.AreaM2 > p.ToleranciaSuperposicionM2 || porcentaje > p.ToleranciaSuperposicionPorcentaje)
                return p.SuperposicionBloquea
                    ? Rechazado($"{detalle} Supera la tolerancia de {p.ToleranciaSuperposicionM2:0} m² o {p.ToleranciaSuperposicionPorcentaje:0.##} %.")
                    : ConAlerta($"{detalle} Supera la tolerancia; un verificador debe revisarlo.");

            return Conforme($"Polígono válido de {ctx.AreaPoligonoHa:0.##} ha. {detalle} Está dentro de la tolerancia (error de GPS en el borde).");
        }

        return Conforme($"Polígono válido de {ctx.AreaPoligonoHa:0.##} ha, sin superposiciones.");
    }

    private (int PredioId, double AreaM2)? BuscarMayorSuperposicion(ContextoValidacion ctx, NetTopologySuite.Geometries.Polygon propio, Coordenada centro)
    {
        (int PredioId, double AreaM2)? peor = null;

        var otros = poligonos.Listar(o => o.Vigente && o.PredioId != ctx.Predio.Id && o.Vertices.Count >= 4 && o.Vertices[0] == o.Vertices[^1]);
        foreach (var otro in otros)
        {
            var geometria = Geometrias.CrearProyectado(otro.Vertices, centro);
            if (!geometria.IsValid || !propio.Intersects(geometria))
                continue;

            var area = propio.Intersection(geometria).Area;
            if (area > 0 && (peor is null || area > peor.Value.AreaM2))
                peor = (otro.PredioId, area);
        }

        return peor;
    }
}

/// <summary>Capa 2. ¿De quién es y cuánto mide? Título del INRA, titular y superficie titulada.</summary>
public class ValidadorTitularidad : ValidadorCapa
{
    public override CapaValidacion Capa => CapaValidacion.Titularidad;
    public override int Orden => 2;

    protected override Task<ResultadoCapa> ValidarAsync(ContextoValidacion ctx)
    {
        var titulo = ctx.Documentos.FirstOrDefault(d => d.Tipo == TipoDocumento.TituloPropiedad);
        if (titulo is null)
            return Task.FromResult(ConAlerta("No se adjuntó el título de propiedad del INRA."));

        var problemas = new List<string>();

        if (!string.IsNullOrWhiteSpace(titulo.TitularCi) && ctx.Productor is not null &&
            titulo.TitularCi.Trim() != ctx.Productor.Ci.Trim())
            problemas.Add($"El titular del título (CI {titulo.TitularCi}) no coincide con el declarante (CI {ctx.Productor.Ci}).");

        if (titulo.SuperficieHa is double titulada && ctx.AreaPoligonoHa > titulada)
            problemas.Add($"La superficie del polígono ({ctx.AreaPoligonoHa:0.##} ha) supera la del título ({titulada:0.##} ha) en {ctx.AreaPoligonoHa - titulada:0.##} ha.");

        return Task.FromResult(problemas.Count > 0
            ? ConAlerta(string.Join(" ", problemas))
            : Conforme("El título corresponde al declarante y cubre la superficie del polígono."));
    }
}

/// <summary>
/// Capa 3. ¿Qué se puede hacer ahí? Delega la evaluación en una estrategia (patrón Strategy):
/// la cadena define el orden entre capas; la estrategia define cómo se evalúa esta capa.
/// </summary>
public class ValidadorUsoSuelo(SelectorEstrategiaUsoSuelo selector) : ValidadorCapa
{
    public override CapaValidacion Capa => CapaValidacion.UsoSuelo;
    public override int Orden => 3;

    protected override Task<ResultadoCapa> ValidarAsync(ContextoValidacion ctx)
    {
        var estrategia = selector.Seleccionar(ctx);
        var evaluacion = estrategia.Evaluar(ctx);

        return Task.FromResult(new ResultadoCapa
        {
            Capa = Capa,
            Estado = evaluacion.Estado == EstadoUsoSuelo.NoAutorizado ? EstadoCapa.Alerta : EstadoCapa.Conforme,
            UsoSuelo = evaluacion.Estado,
            Mensaje = $"{estrategia.Nombre}: {evaluacion.Mensaje}"
        });
    }
}

/// <summary>
/// Capa 4. ¿Qué pasó ahí desde el 31/12/2020? Es probabilística: genera una alerta, nunca un rechazo.
/// Recibe la interfaz IProveedorCobertura sin saber si detrás hay un proxy, el adaptador o la simulación.
/// </summary>
public class ValidadorCobertura(IProveedorCobertura proveedor) : ValidadorCapa
{
    public override CapaValidacion Capa => CapaValidacion.Cobertura;
    public override int Orden => 4;

    protected override async Task<ResultadoCapa> ValidarAsync(ContextoValidacion ctx)
    {
        if (ctx.Poligono.EsPunto)
            return Conforme("El predio se registró con un punto; el análisis satelital se hace sobre polígonos.");

        var corte = ctx.Parametros.FechaCorte;
        ResultadoCobertura r;
        try
        {
            r = await proveedor.AnalizarAsync(ctx.Poligono.Vertices, corte, ctx.Cancelacion);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ConAlerta($"No se pudo completar el análisis satelital ({ex.Message}). Un verificador debe revisarlo.");
        }

        var perdida = r.BosquePorcentajeCorte - r.BosquePorcentajeActual;
        if (perdida > ctx.Parametros.PerdidaCoberturaAlertaPorcentaje)
            return ConAlerta($"Posible pérdida de cobertura boscosa de {perdida:0.#} puntos porcentuales desde el {corte:dd/MM/yyyy} " +
                             $"({r.BosquePorcentajeCorte:0.#} % a {r.BosquePorcentajeActual:0.#} %, imagen del {r.FechaImagenActual:dd/MM/yyyy}). " +
                             "Es una alerta para revisión humana: la precisión satelital varía entre ±5 % y ±20 %.");

        return Conforme($"Sin pérdida significativa de cobertura desde el {corte:dd/MM/yyyy}: " +
                        $"{r.BosquePorcentajeCorte:0.#} % de bosque entonces y {r.BosquePorcentajeActual:0.#} % ahora. Fuente: {r.Fuente}.");
    }
}
