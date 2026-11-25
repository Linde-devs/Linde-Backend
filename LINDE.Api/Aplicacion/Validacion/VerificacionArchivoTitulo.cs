using LINDE.Api.Dominio;
using LINDE.Api.Importacion;
using LINDE.Api.Servicios;

namespace LINDE.Api.Validacion;

public enum LecturaArchivo
{
    /// <summary>El número del título aparece en el texto del archivo.</summary>
    NumeroEncontrado,

    /// <summary>El PDF tiene texto legible, pero el número no aparece: puede ser otro documento.</summary>
    NumeroNoEncontrado,

    /// <summary>Es una imagen o un PDF escaneado: LINDE no puede leerlo y debe revisarlo una persona.</summary>
    NoLegible,

    SinArchivo
}

/// <summary>
/// RF-16. LINDE no está conectado al INRA y no hace reconocimiento de texto en imágenes, así que no puede saber si un
/// archivo es de verdad el título. Lo que sí hace: si el archivo es un PDF con texto, busca en él el número del título.
/// Una imagen o un escaneo queda como "no legible" y la capa de titularidad avisa para que se revise a mano.
/// </summary>
public class VerificacionArchivoTitulo(IAlmacenArchivos? almacen = null)
{
    public LecturaArchivo Verificar(Documento titulo)
    {
        if (titulo.ArchivoClave is null || almacen is null)
            return LecturaArchivo.SinArchivo;
        if (titulo.ArchivoTipo != "application/pdf")
            return LecturaArchivo.NoLegible;

        byte[] datos;
        try
        {
            using var archivo = almacen.Abrir(titulo.ArchivoClave);
            using var copia = new MemoryStream();
            archivo.CopyTo(copia);
            datos = copia.ToArray();
        }
        catch (Exception e) when (e is IOException or RecursoNoEncontradoException)
        {
            return LecturaArchivo.SinArchivo;
        }

        var texto = TextoPdf.Extraer(datos);
        if (!TextoPdf.EsLegible(texto))
            return LecturaArchivo.NoLegible;

        var numero = NumeroTitulo.Compacto(titulo.Numero ?? "");
        return numero.Length > 0 && NumeroTitulo.Compacto(texto).Contains(numero)
            ? LecturaArchivo.NumeroEncontrado
            : LecturaArchivo.NumeroNoEncontrado;
    }
}
