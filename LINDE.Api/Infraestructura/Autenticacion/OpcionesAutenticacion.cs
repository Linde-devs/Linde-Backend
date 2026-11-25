using System.Text;
using LINDE.Api.Dominio;
using Microsoft.IdentityModel.Tokens;

namespace LINDE.Api.Autenticacion;

/// <summary>Cuentas que se crean al iniciar la API (sección "CuentasIniciales" de appsettings), para que existan los roles internos.</summary>
public class CuentaInicial
{
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string Ci { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Contrasena { get; set; } = "";
    public RolUsuario Rol { get; set; }
}

/// <summary>Configuración del token (sección "Jwt" de appsettings).</summary>
public class OpcionesJwt
{
    public string Emisor { get; set; } = "LINDE.Api";
    public string Audiencia { get; set; } = "LINDE.Frontend";

    /// <summary>Clave secreta para firmar los tokens. Mínimo 32 caracteres. En producción, fuera del repositorio.</summary>
    public string Clave { get; set; } = "";

    public int MinutosExpiracion { get; set; } = 480;

    public SymmetricSecurityKey ClaveFirma()
    {
        if (Encoding.UTF8.GetByteCount(Clave) < 32)
            throw new InvalidOperationException(
                "Falta la clave del token (Jwt:Clave) o tiene menos de 32 caracteres. Configúrala en appsettings.Development.json.");

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Clave));
    }
}
