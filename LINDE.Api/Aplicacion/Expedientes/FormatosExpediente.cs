using System.Text.Json;

namespace LINDE.Api.Expedientes;

/// <summary>Representaciones del mismo expediente. El PDF lo genera el navegador desde la vista imprimible del frontend.</summary>
public static class FormatosExpediente
{
    /// <summary>GeoJSON estándar (RNF-06) para el comprador: una Feature con el polígono y los datos del expediente.</summary>
    public static string AGeoJson(Expediente e)
    {
        object geometria = e.Vertices.Count == 1
            ? new { type = "Point", coordinates = new[] { e.Vertices[0].Lon, e.Vertices[0].Lat } }
            : new { type = "Polygon", coordinates = new[] { e.Vertices.Select(v => new[] { v.Lon, v.Lat }).ToArray() } };

        var feature = new
        {
            type = "Feature",
            geometry = geometria,
            properties = new
            {
                codigo = e.Codigo,
                predio = e.Predio,
                departamento = e.Departamento,
                municipio = e.Municipio,
                productor = e.Productor,
                areaHa = e.AreaPoligonoHa,
                versionPoligono = e.VersionPoligono,
                fechaGeneracion = e.FechaGeneracion,
                estado = e.Estado.ToString(),
                alertas = e.Alertas.Select(a => new { capa = a.Capa.ToString(), prioridad = a.Prioridad.ToString(), a.Mensaje }),
                validaciones = e.Validaciones.Select(v => new { capa = v.Capa.ToString(), estado = v.Estado.ToString(), v.Mensaje }),
                inspecciones = e.Inspecciones.Select(i => new
                {
                    fecha = i.Fecha.ToString("yyyy-MM-dd"), tecnico = i.Tecnico, resultado = i.Resultado.ToString(), comentario = i.Comentario
                })
            }
        };

        return JsonSerializer.Serialize(feature, new JsonSerializerOptions { WriteIndented = true });
    }
}
