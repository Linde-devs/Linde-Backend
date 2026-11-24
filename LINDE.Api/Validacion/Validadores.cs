using LINDE.Api.Cobertura;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;
using LINDE.Api.Validacion.UsoSuelo;

namespace LINDE.Api.Validacion;

/// <summary>
/// Capa 1. ¿El polígono es válido y único? Cerrado, sin lados cruzados, sin vértices duplicados,
/// obligatorio para predios de más de 4 ha, y sin superposición con otros predios.
/// Es la única capa que puede cortar la cadena.
/// </summary>
public class ValidadorGeometrico(IRepositorio<Poligono> poligonos, IRepositorio<Predio>? predios = null) : ValidadorCapa
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

        // Las mismas reglas que se aplican al cargar el polígono (RF-03), por si se registró antes de esa revisión.
        if (RevisionGeometrica.Revisar(v) is { } problema)
            return Rechazado(problema.Mensaje);

        var centro = Geometrias.Centro(v);
        var propio = Geometrias.CrearProyectado(v, centro);
        ctx.AreaPoligonoHa = propio.Area / 10_000d;

        var peor = BuscarMayorSuperposicion(ctx, propio, centro);
        if (peor is { } s)
        {
            var porcentaje = s.AreaM2 / propio.Area * 100;
            var detalle = $"Se superpone {s.AreaM2:0} m² ({porcentaje:0.##} %) con el predio {s.PredioId}{NombrePredio(s.PredioId, ctx.Predio.ProductorId)}.";

            if (s.AreaM2 > p.ToleranciaSuperposicionM2 || porcentaje > p.ToleranciaSuperposicionPorcentaje)
                return p.SuperposicionBloquea
                    ? Rechazado($"{detalle} Supera la tolerancia de {p.ToleranciaSuperposicionM2:0} m² o {p.ToleranciaSuperposicionPorcentaje:0.##} %.")
                    : ConAlerta($"{detalle} Supera la tolerancia; conviene que un técnico lo revise.");

            return Conforme($"Polígono válido de {ctx.AreaPoligonoHa:0.##} ha. {detalle} Está dentro de la tolerancia (error de GPS en el borde).");
        }

        return Conforme($"Polígono válido de {ctx.AreaPoligonoHa:0.##} ha, sin superposiciones.");
    }

    /// <summary>
    /// RF-06 pide mostrar con qué predio se superpone. El nombre solo se muestra si el otro predio es del mismo
    /// productor: los datos de otros productores no se revelan.
    /// </summary>
    private string NombrePredio(int predioId, int productorId) =>
        predios?.Obtener(predioId) is { } otro
            ? otro.ProductorId == productorId ? $" ({otro.Nombre})" : " (de otro productor)"
            : "";

    private (int PredioId, double AreaM2)? BuscarMayorSuperposicion(ContextoValidacion ctx, NetTopologySuite.Geometries.Polygon propio, Coordenada centro)
    {
        (int PredioId, double AreaM2)? peor = null;

        // Con PostgreSQL esta consulta se traduce a ST_Intersects y usa el índice espacial GiST (RF-06, RNF-02):
        // la base devuelve solo los polígonos que tocan al del predio, no todos los registrados.
        // El área superpuesta se calcula después en metros, con la proyección de área equivalente (RNF-09).
        var geometria = Geometrias.Crear(ctx.Poligono.Vertices);
        var predioId = ctx.Predio.Id;
        var otros = poligonos.Listar(o => o.Vigente && o.PredioId != predioId && o.Geometria.Intersects(geometria));

        foreach (var otro in otros)
        {
            if (otro.EsPunto || RevisionGeometrica.Revisar(otro.Vertices) is not null)
                continue;

            var proyectado = Geometrias.CrearProyectado(otro.Vertices, centro);
            if (!propio.Intersects(proyectado))
                continue;

            var area = propio.Intersection(proyectado).Area;
            if (area > 0 && (peor is null || area > peor.Value.AreaM2))
                peor = (otro.PredioId, area);
        }

        return peor;
    }
}

/// <summary>
/// Capa 2. ¿De quién es y cuánto mide? Título del INRA, titular y superficie titulada.
/// Si el administrador cargó la capa de títulos del INRA (CU-11), además cruza el polígono con ella (RF-07):
/// que caiga sobre un predio titulado, que sea el mismo título que registró el productor y que no se salga de él.
/// Sin capa cargada, compara solo con los datos del título que declaró el productor.
/// </summary>
public class ValidadorTitularidad(IConsultaCapas? capas = null) : ValidadorCapa
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

        var cruce = CruzarConCapa(ctx, titulo, problemas);

        return Task.FromResult(problemas.Count > 0
            ? ConAlerta(string.Join(" ", problemas))
            : Conforme($"El título corresponde al declarante y cubre la superficie del polígono.{cruce}"));
    }

    /// <summary>RF-07 con la capa del INRA. Agrega los problemas encontrados y devuelve la nota para el resultado conforme.</summary>
    private string CruzarConCapa(ContextoValidacion ctx, Documento titulo, List<string> problemas)
    {
        if (capas is null || !capas.HayCapa(TipoCapa.TitulosInra))
            return " No hay capa del INRA cargada: se comparó con los datos del título que registró el productor.";

        var coincidencia = capas.MejorCoincidencia(TipoCapa.TitulosInra, ctx.Poligono.Vertices);
        if (coincidencia is null)
        {
            problemas.Add("El polígono no coincide con ningún predio titulado de la capa del INRA.");
            return "";
        }

        var codigo = coincidencia.Elemento.Codigo;
        if (!string.IsNullOrWhiteSpace(titulo.Numero) && !MismoCodigo(titulo.Numero, codigo))
            problemas.Add($"Según la capa del INRA, el polígono está sobre el título {codigo}, pero el título registrado es {titulo.Numero}.");

        // No se muestra el CI que figura en la capa: es un dato de otra persona.
        if (coincidencia.Elemento.TitularCi is { } ci && ctx.Productor is not null && ci.Trim() != ctx.Productor.Ci.Trim())
            problemas.Add($"Según la capa del INRA, el titular del título {codigo} no es el declarante.");

        var p = ctx.Parametros;
        var tolerancia = Math.Max(p.ToleranciaSuperposicionM2 / 10_000d, ctx.AreaPoligonoHa * p.ToleranciaSuperposicionPorcentaje / 100);
        if (coincidencia.AreaFueraHa > tolerancia)
            problemas.Add($"{coincidencia.AreaFueraHa:0.##} ha del polígono quedan fuera del título {codigo} según la capa del INRA.");

        return $" Coincide con el título {codigo} de la capa del INRA.";
    }

    /// <summary>Compara números de título sin espacios, guiones ni mayúsculas ("SPP-NAL-070167" = "sppnal070167").</summary>
    public static bool MismoCodigo(string a, string b)
    {
        static string Limpio(string t) => new(t.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return Limpio(a) == Limpio(b);
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
            return ConAlerta($"No se pudo completar el análisis satelital ({ex.Message}). Conviene que un técnico lo revise.");
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
