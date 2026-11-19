namespace LINDE.Api.Dominio.Estados;

public interface IEstadoPredio
{
    EstadoExpediente Valor { get; }
    void VerificarEdicion();
    void VerificarEnvio();
    EstadoExpediente EnviarARevision(bool hayAlertas);
    EstadoExpediente Aprobar();
    EstadoExpediente Observar();
    void VerificarGeneracionExpediente();
}

public abstract class EstadoPredioBase : IEstadoPredio
{
    public abstract EstadoExpediente Valor { get; }
    protected abstract string Nombre { get; }

    public virtual void VerificarEdicion() => throw NoPermitido("editar");
    public virtual void VerificarEnvio() => throw NoPermitido("enviar a revisión");
    public virtual EstadoExpediente EnviarARevision(bool hayAlertas) => throw NoPermitido("enviar a revisión");
    public virtual EstadoExpediente Aprobar() => throw NoPermitido("aprobar");
    public virtual EstadoExpediente Observar() => throw NoPermitido("observar");
    public virtual void VerificarGeneracionExpediente() => throw NoPermitido("generar el expediente de");

    protected OperacionNoPermitidaException NoPermitido(string operacion) =>
        new($"No se puede {operacion} un predio en estado \"{Nombre}\".");
}

public sealed class EstadoRegistrado : EstadoPredioBase
{
    public override EstadoExpediente Valor => EstadoExpediente.Registrado;
    protected override string Nombre => "Registrado";

    public override void VerificarEdicion() { }
    public override void VerificarEnvio() { }

    public override EstadoExpediente EnviarARevision(bool hayAlertas) =>
        hayAlertas ? EstadoExpediente.EnRevision : EstadoExpediente.Aprobado;
}

public sealed class EstadoEnRevision : EstadoPredioBase
{
    public override EstadoExpediente Valor => EstadoExpediente.EnRevision;
    protected override string Nombre => "En revisión";

    public override EstadoExpediente Aprobar() => EstadoExpediente.Aprobado;
    public override EstadoExpediente Observar() => EstadoExpediente.Observado;
}

public sealed class EstadoObservado : EstadoPredioBase
{
    public override EstadoExpediente Valor => EstadoExpediente.Observado;
    protected override string Nombre => "Observado";

    public override void VerificarEdicion() { }
    public override void VerificarEnvio() { }

    public override EstadoExpediente EnviarARevision(bool hayAlertas) =>
        hayAlertas ? EstadoExpediente.EnRevision : EstadoExpediente.Aprobado;
}

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
        [EstadoExpediente.EnRevision] = new EstadoEnRevision(),
        [EstadoExpediente.Observado] = new EstadoObservado(),
        [EstadoExpediente.Aprobado] = new EstadoAprobado()
    };

    public static IEstadoPredio Para(EstadoExpediente valor) => Estados[valor];
}
