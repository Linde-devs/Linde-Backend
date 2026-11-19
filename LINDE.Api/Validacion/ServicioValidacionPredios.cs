using LINDE.Api.Auditoria;
using LINDE.Api.Configuracion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Validacion;

/// <summary>CU-06 y CU-07: una sola operación para enviar un predio a revisión.</summary>
public interface IServicioValidacionPredios
{
    Task<ResultadoValidacion> EnviarARevisionAsync(int predioId, CancellationToken ct = default);
    IReadOnlyList<ResultadoValidacion> Historial(int predioId);
}

/// <summary>
/// Patrón Facade: el controlador solo llama a EnviarARevisionAsync. Por dentro, la fachada coordina en orden:
/// verificar el estado, ejecutar la cadena de validadores, guardar el resultado, generar alertas,
/// cambiar el estado del predio y registrar todo en la bitácora.
/// Une los tres tipos de patrones: recibe los validadores que crea el contenedor (Factory Method),
/// los ejecuta en cadena (Chain of Responsibility) y el predio decide sus transiciones (State).
/// Solo coordina: las reglas de cada capa siguen dentro de su validador.
/// </summary>
public class ServicioValidacionPredios(
    IRepositorio<Predio> predios,
    IRepositorio<Poligono> poligonos,
    IRepositorio<Productor> productores,
    IRepositorio<Documento> documentos,
    IRepositorio<ResultadoValidacion> resultados,
    IRepositorio<Alerta> alertas,
    IServicioParametros parametros,
    IEnumerable<ValidadorCapa> validadores,
    IBitacora bitacora) : IServicioValidacionPredios
{
    public async Task<ResultadoValidacion> EnviarARevisionAsync(int predioId, CancellationToken ct = default)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        predio.VerificarEnvio();

        var poligono = poligonos.Listar(p => p.PredioId == predioId && p.Vigente).FirstOrDefault()
                       ?? throw new DatosInvalidosException("El predio no tiene un polígono ni un punto registrado.");

        var contexto = new ContextoValidacion
        {
            Predio = predio,
            Poligono = poligono,
            Productor = productores.Obtener(predio.ProductorId),
            Documentos = documentos.Listar(d => d.PredioId == predioId),
            Parametros = parametros.Actuales,
            Cancelacion = ct
        };

        await ValidadorCapa.ArmarCadena(validadores).ProcesarAsync(contexto);

        var resultado = new ResultadoValidacion
        {
            PredioId = predioId,
            VersionPoligono = poligono.Version,
            Capas = contexto.Resultados,
            Cortado = contexto.Cortado
        };

        if (contexto.Cortado)
        {
            resultado.EstadoResultante = predio.Estado;
            resultado.Mensaje = $"La capa geométrica rechazó el predio: {contexto.Resultados[^1].Mensaje} " +
                                "El predio vuelve al productor para corregirlo; no se ejecutaron las demás capas.";
        }
        else
        {
            CerrarAlertasAnteriores(predioId);
            var nuevas = contexto.Resultados.Where(r => r.Estado == EstadoCapa.Alerta).ToList();
            foreach (var capa in nuevas)
                alertas.Agregar(new Alerta { PredioId = predioId, Capa = capa.Capa, Prioridad = Prioridad(capa.Capa), Mensaje = capa.Mensaje });

            predio.EnviarARevision(hayAlertas: nuevas.Count > 0);
            predios.Actualizar(predio);

            resultado.EstadoResultante = predio.Estado;
            resultado.Mensaje = nuevas.Count == 0
                ? "Las cuatro capas están conformes. El predio quedó aprobado."
                : $"Se generaron {nuevas.Count} alerta(s). El predio pasó a revisión del verificador.";
        }

        resultados.Agregar(resultado);
        bitacora.Registrar("Enviar a revisión", "Predio", predioId.ToString(), null,
            $"{resultado.EstadoResultante}: {resultado.Mensaje}");

        return resultado;
    }

    public IReadOnlyList<ResultadoValidacion> Historial(int predioId) =>
        resultados.Listar(r => r.PredioId == predioId).OrderByDescending(r => r.Fecha).ToList();

    private void CerrarAlertasAnteriores(int predioId)
    {
        foreach (var alerta in alertas.Listar(a => a.PredioId == predioId && !a.Resuelta))
        {
            alerta.Resuelta = true;
            alertas.Actualizar(alerta);
        }
    }

    private static PrioridadAlerta Prioridad(CapaValidacion capa) => capa switch
    {
        CapaValidacion.UsoSuelo or CapaValidacion.Cobertura => PrioridadAlerta.Alta,
        CapaValidacion.Titularidad => PrioridadAlerta.Media,
        _ => PrioridadAlerta.Media
    };
}
