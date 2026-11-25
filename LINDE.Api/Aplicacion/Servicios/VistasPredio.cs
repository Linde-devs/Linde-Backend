using LINDE.Api.Contratos;
using LINDE.Api.Datos;
using LINDE.Api.Dominio;
using LINDE.Api.Geometria;

namespace LINDE.Api.Servicios;

/// <summary>Arma la vista de un predio para el frontend: nombre del productor y datos del polígono vigente.</summary>
public class VistasPredio(IRepositorio<Productor> productores, IRepositorio<Poligono> poligonos, IRepositorio<Documento> documentos)
{
    public PredioVista Crear(Predio p)
    {
        var productor = productores.Obtener(p.ProductorId);
        var poligono = poligonos.Listar(x => x.PredioId == p.Id && x.Vigente).FirstOrDefault();
        var titulo = documentos.Listar(d => d.PredioId == p.Id && d.Tipo == TipoDocumento.TituloPropiedad).FirstOrDefault();

        double? area = null;
        if (poligono is { EsPunto: false } && RevisionGeometrica.Revisar(poligono.Vertices) is null)
            area = Math.Round(Geometrias.AreaHectareas(poligono.Vertices), 2);

        return new PredioVista(
            p.Id, p.ProductorId,
            productor is null ? "" : $"{productor.Nombre} {productor.Apellido}",
            p.Nombre, p.Municipio, p.Cultivo, p.SuperficieDeclaradaHa, p.NumeroTituloInra, p.DesmonteDeclaradoHa,
            p.Estado, p.FechaRegistro,
            poligono is not null, poligono?.EsPunto ?? false, area,
            titulo?.TitularCi, titulo?.SuperficieHa, p.Departamento);
    }
}
