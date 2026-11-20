namespace LINDE.Api.Dominio.Estados;

/// <summary>
/// Patrón State. Cada estado decide qué operaciones permite el predio.
/// LINDE no tiene verificadores ni cuentas del SENASAG: nadie aprueba ni observa dentro del sistema.
/// Un predio queda "Aprobado" solo si las cuatro capas están conformes; si hay alertas queda "Con alertas"
/// y el productor puede corregirlo y reenviarlo, o generar su expediente con las alertas para mostrarlo.
/// </summary>
public interface IEstadoPredio
{
    EstadoExpediente Valor { get; }
    void VerificarEdicion();
    void VerificarEnvio();
    EstadoExpediente EnviarARevision(bool hayAlertas);
    void VerificarGeneracionExpediente();
}

public abstract class EstadoPredioBase : IEstadoPredio
{
    public abstract EstadoExpediente Valor { get; }
    protected abstract string Nombre { get; }

    public virtual void VerificarEdicion() => throw NoPermitido("editar");
    public virtual void VerificarEnvio() => throw NoPermitido("enviar a revisión");
    public virtual EstadoExpediente EnviarARevision(bool hayAlertas) => throw NoPermitido("enviar a revisión");
    public virtual void VerificarGeneracionExpediente() => throw NoPermitido("generar el expediente de");

    protected OperacionNoPermitidaException NoPermitido(string operacion) =>
        new($"No se puede {operacion} un predio en estado \"{Nombre}\".");

    /// <summary>Resultado de un envío: sin alertas, aprobado; con alertas, con alertas.</summary>
    protected static EstadoExpediente Resultado(bool hayAlertas) =>
        hayAlertas ? EstadoExpediente.ConAlertas : EstadoExpediente.Aprobado;
}

/// <summary>Recién registrado o devuelto por la capa geométrica: se edita y se envía.</summary>
public sealed class EstadoRegistrado : EstadoPredioBase
{
    public override EstadoExpediente Valor => EstadoExpediente.Registrado;
    protected override string Nombre => "Registrado";

    public override void VerificarEdicion() { }
    public override void VerificarEnvio() { }
    public override EstadoExpediente EnviarARevision(bool hayAlertas) => Resultado(hayAlertas);
}

/// <summary>
/// Pasó la geometría pero alguna capa generó alertas. El productor puede corregir y reenviar,
/// o generar el expediente con las alertas para mostrarlo a un técnico fuera del sistema.
/// </summary>
public sealed class EstadoConAlertas : EstadoPredioBase
{
    public override EstadoExpediente Valor => EstadoExpediente.ConAlertas;
    protected override string Nombre => "Con alertas";

    public override void VerificarEdicion() { }
    public override void VerificarEnvio() { }
    public override EstadoExpediente EnviarARevision(bool hayAlertas) => Resultado(hayAlertas);
    public override void VerificarGeneracionExpediente() { }
}

/// <summary>Las cuatro capas conformes. Es final: solo permite generar el expediente.</summary>
public sealed class EstadoAprobado : EstadoPredioBase
{
    public override EstadoExpediente Valor => EstadoExpediente.Aprobado;
    protected override string Nombre => "Aprobado";

    public override void VerificarGeneracionExpediente() { }
}

public static class EstadosPredio
{
    private static readonly Dictionary<EstadoExpediente, IEstadoPredio> Estados = new()
    {
        [EstadoExpediente.Registrado] = new EstadoRegistrado(),
        [EstadoExpediente.ConAlertas] = new EstadoConAlertas(),
        [EstadoExpediente.Aprobado] = new EstadoAprobado()
    };

    public static IEstadoPredio Para(EstadoExpediente valor) => Estados[valor];
}
