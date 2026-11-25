using LINDE.Api.Dominio;

namespace LINDE.Api.Cobertura;

/// <summary>
/// Recuadro de la imagen: el predio con un margen del 25 % alrededor (para ver el entorno) y, si es un punto o un
/// predio muy chico, al menos unos 600 m de lado. El ancho en píxeles sigue la forma real del recuadro en metros.
/// </summary>
public record MarcoImagen(double Oeste, double Sur, double Este, double Norte, int Ancho, int Alto)
{
    public const int LadoMaximo = 640;

    public static MarcoImagen Calcular(IReadOnlyList<Coordenada> vertices)
    {
        var oeste = vertices.Min(v => v.Lon); var este = vertices.Max(v => v.Lon);
        var sur = vertices.Min(v => v.Lat); var norte = vertices.Max(v => v.Lat);
        var latMedia = (sur + norte) / 2;
        var coseno = Math.Cos(latMedia * Math.PI / 180);
        const double minimoGrados = 600d / 111_320d;

        var margenLon = Math.Max((este - oeste) * 0.25, (minimoGrados / coseno - (este - oeste)) / 2);
        var margenLat = Math.Max((norte - sur) * 0.25, (minimoGrados - (norte - sur)) / 2);
        oeste -= margenLon; este += margenLon; sur -= margenLat; norte += margenLat;

        var anchoMetros = (este - oeste) * coseno; var altoMetros = norte - sur;
        var (ancho, alto) = anchoMetros >= altoMetros
            ? (LadoMaximo, Math.Max(1, (int)Math.Round(LadoMaximo * altoMetros / anchoMetros)))
            : (Math.Max(1, (int)Math.Round(LadoMaximo * anchoMetros / altoMetros)), LadoMaximo);
        return new MarcoImagen(Math.Round(oeste, 6), Math.Round(sur, 6), Math.Round(este, 6), Math.Round(norte, 6), ancho, alto);
    }
}
