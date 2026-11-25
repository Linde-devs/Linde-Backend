using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace LINDE.Api.Importacion;

/// <summary>
/// Saca el texto de un PDF generado digitalmente, sin bibliotecas externas: recorre los "stream" del archivo,
/// descomprime los que usan FlateDecode (el caso común) y junta las cadenas que se escriben con Tj y TJ.
/// No hace reconocimiento de caracteres: un PDF escaneado (solo imágenes) o con fuentes de dos bytes devuelve poco o
/// nada legible. Quien lo usa decide con <see cref="EsLegible"/> si el texto sirve para comparar.
/// </summary>
public static partial class TextoPdf
{
    public static string Extraer(byte[] pdf)
    {
        var texto = new StringBuilder();
        var latin = Encoding.Latin1.GetString(pdf);

        foreach (Match m in InicioStream().Matches(latin))
        {
            var inicio = m.Index + m.Length;
            var fin = latin.IndexOf("endstream", inicio, StringComparison.Ordinal);
            if (fin < 0) break;

            var diccionario = latin[Math.Max(0, latin.LastIndexOf("<<", m.Index, StringComparison.Ordinal))..m.Index];
            var datos = pdf.AsSpan(inicio, fin - inicio).ToArray();
            var contenido = diccionario.Contains("/FlateDecode") ? Descomprimir(datos) : datos;
            if (contenido is not null)
                Cadenas(Encoding.Latin1.GetString(contenido), texto);
        }
        return texto.ToString();
    }

    /// <summary>Hay texto real si se juntaron al menos 30 letras o dígitos y casi todo es imprimible.</summary>
    public static bool EsLegible(string texto)
    {
        if (texto.Length == 0) return false;
        var alfanumericos = texto.Count(char.IsLetterOrDigit);
        var imprimibles = texto.Count(c => !char.IsControl(c) && c < 0x250);
        return alfanumericos >= 30 && imprimibles >= texto.Length * 0.85;
    }

    private static byte[]? Descomprimir(byte[] datos)
    {
        try
        {
            using var entrada = new ZLibStream(new MemoryStream(datos), CompressionMode.Decompress);
            using var salida = new MemoryStream();
            entrada.CopyTo(salida);
            return salida.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Junta las cadenas (literales entre paréntesis y hexadecimales) de los operadores Tj y TJ.</summary>
    private static void Cadenas(string contenido, StringBuilder texto)
    {
        if (!contenido.Contains("Tj") && !contenido.Contains("TJ")) return;

        for (var i = 0; i < contenido.Length; i++)
        {
            var c = contenido[i];
            if (c == '(')
            {
                var profundidad = 1;
                for (i++; i < contenido.Length && profundidad > 0; i++)
                {
                    var d = contenido[i];
                    if (d == '\\' && i + 1 < contenido.Length)
                    {
                        i++;
                        texto.Append(contenido[i] switch { 'n' => ' ', 'r' => ' ', 't' => ' ', var e => e });
                    }
                    else if (d == '(') { profundidad++; texto.Append(d); }
                    else if (d == ')') { if (--profundidad > 0) texto.Append(d); }
                    else texto.Append(d);
                }
                i--;
            }
            else if (c == '<' && i + 1 < contenido.Length && contenido[i + 1] != '<')
            {
                var fin = contenido.IndexOf('>', i);
                if (fin < 0) break;
                var hex = contenido[(i + 1)..fin].Where(Uri.IsHexDigit).ToArray();
                for (var k = 0; k + 1 < hex.Length; k += 2)
                    texto.Append((char)Convert.ToByte(new string(hex, k, 2), 16));
                i = fin;
            }
            else if (c == ']' || (c == 'T' && i + 1 < contenido.Length && contenido[i + 1] == '*'))
            {
                texto.Append(' ');
            }
        }
        texto.Append(' ');
    }

    [GeneratedRegex(@"stream\r?\n")]
    private static partial Regex InicioStream();
}
