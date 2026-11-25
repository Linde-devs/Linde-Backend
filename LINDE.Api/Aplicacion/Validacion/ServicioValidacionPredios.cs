using LINDE.Api.Auditoria;
using LINDE.Api.Autenticacion;
using LINDE.Api.Configuracion;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;

namespace LINDE.Api.Validacion;

/// <summary>CU-06 (enviar a revisión) y CU-07 (validar que el predio tenga todo lo necesario).</summary>
public interface IServicioValidacionPredios
{
    Task<ResultadoValidacion> EnviarARevisionAsync(int predioId, CancellationToken ct = default);

    /// <summary>
    /// CU-07. Revisa que el predio tenga todos los datos y documentos necesarios para enviarlo, sin juzgar si son
    /// correctos (eso lo hacen las cuatro capas al enviarlo). No guarda nada ni cambia el estado.
    /// </summary>
    RevisionRequisitos RevisarRequisitos(int predioId);

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
    IBitacora bitacora,
    ControlAcceso acceso) : IServicioValidacionPredios
{
    public async Task<ResultadoValidacion> EnviarARevisionAsync(int predioId, CancellationToken ct = default)
    {
        var predio = ObtenerParaValidar(predioId);
        predio.VerificarEnvio();

        // Primero se exige que esté todo (CU-07); recién entonces las capas revisan si los datos son correctos.
        var requisitos = Requisitos(predio);
        if (!requisitos.Completo)
            throw new DatosInvalidosException("No se puede enviar a revisión: faltan requisitos. " +
                                              string.Join(" ", requisitos.Pendientes.Select(r => $"{r.Nombre}: {r.Detalle}")));

        var (poligono, contexto) = await EjecutarCadenaAsync(predio, ct);

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
                : $"Se generaron {nuevas.Count} alerta(s). El predio quedó con alertas: revísalas en la Vista para revisión. " +
                  "Puedes corregir el predio y reenviarlo, o generar su expediente con las alertas.";
        }

        resultados.Agregar(resultado);
        bitacora.Registrar("Enviar a revisión", "Predio", predioId.ToString(), null,
            $"{resultado.EstadoResultante}: {resultado.Mensaje}");

        return resultado;
    }

    public RevisionRequisitos RevisarRequisitos(int predioId) => Requisitos(ObtenerParaValidar(predioId));

    private RevisionRequisitos Requisitos(Predio predio) =>
        RequisitosEnvio.Revisar(
            predio,
            poligonos.Listar(p => p.PredioId == predio.Id && p.Vigente).FirstOrDefault(),
            documentos.Listar(d => d.PredioId == predio.Id),
            parametros.Actuales);

    private Predio ObtenerParaValidar(int predioId)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        acceso.VerificarPredio(predio);
        return predio;
    }

    /// <summary>Arma el contexto y recorre la cadena de validadores. Ningún validador guarda datos.</summary>
    private async Task<(Poligono Poligono, ContextoValidacion Contexto)> EjecutarCadenaAsync(Predio predio, CancellationToken ct)
    {
        var poligono = poligonos.Listar(p => p.PredioId == predio.Id && p.Vigente).FirstOrDefault()
                       ?? throw new DatosInvalidosException("El predio no tiene un polígono ni un punto registrado.");

        var contexto = new ContextoValidacion
        {
            Predio = predio,
            Poligono = poligono,
            Productor = productores.Obtener(predio.ProductorId),
            Documentos = documentos.Listar(d => d.PredioId == predio.Id),
            Parametros = parametros.Actuales,
            Cancelacion = ct
        };

        await ValidadorCapa.ArmarCadena(validadores).ProcesarAsync(contexto);
        return (poligono, contexto);
    }

    public IReadOnlyList<ResultadoValidacion> Historial(int predioId)
    {
        var predio = predios.Obtener(predioId) ?? throw new RecursoNoEncontradoException($"No existe el predio {predioId}.");
        acceso.VerificarPredio(predio);
        return resultados.Listar(r => r.PredioId == predioId).OrderByDescending(r => r.Fecha).ToList();
    }

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
