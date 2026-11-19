using LINDE.Api.Auditoria;
using LINDE.Api.Cobertura;
using LINDE.Api.Dominio;
using LINDE.Api.Validacion;

namespace LINDE.Tests;

/// <summary>Datos y dobles de prueba compartidos.</summary>
public static class Ayudas
{
    /// <summary>Cuadrado de unos 1000 x 1000 m (alrededor de 100 ha) cerca de San Julián.</summary>
    public static List<Coordenada> Cuadrado(double lon = -63.0, double lat = -17.5, double lado = 0.009) => new()
    {
        new(lon, lat),
        new(lon + lado * 1.045, lat),
        new(lon + lado * 1.045, lat + lado),
        new(lon, lat + lado),
        new(lon, lat)
    };

    /// <summary>Polígono en forma de moño: sus lados se cruzan.</summary>
    public static List<Coordenada> Mono() => new()
    {
        new(-63.0, -17.5), new(-62.99, -17.49), new(-62.99, -17.5), new(-63.0, -17.49), new(-63.0, -17.5)
    };

    public static Predio Predio(int id = 1, double superficieHa = 100, double? desmonte = null) =>
        new() { Id = id, ProductorId = 1, Nombre = "El Algarrobo", Municipio = "San Julián", SuperficieDeclaradaHa = superficieHa, DesmonteDeclaradoHa = desmonte };

    public static ContextoValidacion Contexto(Predio predio, List<Coordenada> vertices, params Documento[] documentos) => new()
    {
        Predio = predio,
        Poligono = new Poligono { PredioId = predio.Id, Vertices = vertices },
        Productor = new Productor { Id = 1, Nombre = "Juan", Apellido = "Pérez", Ci = "7845123" },
        Documentos = documentos,
        Parametros = new Parametros()
    };
}

public class UsuarioFijo(string nombre = "prueba") : IUsuarioActual
{
    public string Nombre => nombre;
}

/// <summary>Proveedor de cobertura que cuenta cuántas veces se le llamó y devuelve valores fijos.</summary>
public class CoberturaContadora(double bosqueCorte = 60, double bosqueActual = 58) : IProveedorCobertura
{
    public int Llamadas { get; private set; }

    public Task<ResultadoCobertura> AnalizarAsync(IReadOnlyList<Coordenada> vertices, DateOnly fechaCorte, CancellationToken ct = default)
    {
        Llamadas++;
        return Task.FromResult(new ResultadoCobertura(bosqueCorte, bosqueActual, new DateOnly(2026, 11, 1), "Prueba"));
    }
}
