using System.Text.RegularExpressions;

namespace LINDE.Api.Dominio;

/// <summary>
/// Número del título ejecutorial del INRA: el tipo de título (dos a cinco letras, por ejemplo SPP o PPD), "NAL" y un
/// correlativo de 4 a 7 dígitos. Ejemplo: SPP-NAL-070167. LINDE no está conectado al INRA: revisa la forma del número,
/// no que el título exista. Si el administrador carga la capa del INRA (CU-11), la capa de titularidad lo cruza con ella.
/// </summary>
public static partial class NumeroTitulo
{
    public const string Ejemplo = "SPP-NAL-070167";

    /// <summary>Mayúsculas y un solo guion entre partes: "spp nal 070167" queda "SPP-NAL-070167".</summary>
    public static string Normalizar(string numero) => Separadores().Replace(numero.Trim().ToUpperInvariant(), "-").Trim('-');

    public static bool EsValido(string numeroNormalizado) => Forma().IsMatch(numeroNormalizado);

    /// <summary>Solo letras y dígitos, para buscar el número dentro del texto de un archivo.</summary>
    public static string Compacto(string texto) => NoAlfanumerico().Replace(texto.ToUpperInvariant(), "");

    [GeneratedRegex(@"[\s_\-–]+")]
    private static partial Regex Separadores();

    [GeneratedRegex(@"^[A-Z]{2,5}-NAL-\d{4,7}$")]
    private static partial Regex Forma();

    [GeneratedRegex(@"[^A-Z0-9]")]
    private static partial Regex NoAlfanumerico();
}
