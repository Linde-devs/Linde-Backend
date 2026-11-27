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

    /// <summary>
    /// Clave secreta para firmar los tokens: 32 bytes aleatorios o más, en Base64. Nunca va en el repositorio:
    /// en desarrollo se guarda con dotnet user-secrets ("Jwt:Clave"); en producción, en una variable de entorno.
    /// </summary>
    public string Clave { get; set; } = "";

    public int MinutosExpiracion { get; set; } = 480;

    public SymmetricSecurityKey ClaveFirma()
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(Clave);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("La clave del token (Jwt:Clave) debe estar en Base64. " + ComoConfigurar);
        }

        if (bytes.Length < 32)
            throw new InvalidOperationException("Falta la clave del token (Jwt:Clave) o tiene menos de 32 bytes. " + ComoConfigurar);

        return new SymmetricSecurityKey(bytes);
    }

    private const string ComoConfigurar =
        "Genérala y guárdala con dotnet user-secrets desde la carpeta de LINDE.Api (los pasos están en el README).";
}
