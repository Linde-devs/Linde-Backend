using LINDE.Api.Auditoria;
using LINDE.Api.Dominio;

namespace LINDE.Api.Autenticacion;

/// <summary>
/// RNF-03. Un productor solo puede ver y modificar sus propios predios.
/// El administrador ve todos.
/// Los servicios lo consultan antes de entregar o cambiar un predio.
/// </summary>
public class ControlAcceso(IUsuarioActual usuario)
{
    public bool EsProductor => usuario.Rol == RolUsuario.Productor;

    /// <summary>Para un productor, su id de productor; para los demás roles, null (sin filtro).</summary>
    public int? ProductorPropio => EsProductor ? usuario.ProductorId ?? -1 : null;

    public bool PuedeVer(Predio predio) => !EsProductor || predio.ProductorId == usuario.ProductorId;

    public void VerificarPredio(Predio predio)
    {
        if (!PuedeVer(predio)) throw new AccesoDenegadoException();
    }
}
