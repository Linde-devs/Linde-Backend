using LINDE.Api.Dominio;
using LINDE.Api.Geometria;

namespace LINDE.Api.Validacion;

/// <summary>
/// Un requisito para enviar el predio a revisión. Ruta es la pantalla del frontend donde se completa
/// (predios, poligonos o documentos).
/// </summary>
public record Requisito(string Clave, string Nombre, bool Cumplido, string Detalle, string Ruta);

/// <summary>Resultado del CU-07: si el predio tiene todo lo necesario para enviarlo a revisión.</summary>
public record RevisionRequisitos(int PredioId, bool Completo, IReadOnlyList<Requisito> Requisitos)
{
    public IEnumerable<Requisito> Pendientes => Requisitos.Where(r => !r.Cumplido);
}

/// <summary>
/// CU-07. Revisa que el predio tenga todos los datos y documentos que necesitan las cuatro capas, sin juzgar si son
/// correctos: eso lo hacen las capas al enviarlo (CU-06). La lista cambia según el predio, con las mismas reglas que
/// usan las capas: más de 4 ha exige polígono (RF-05) y fuera de la Ley 741 se exigen las autorizaciones de la ABT.
/// </summary>
public static class RequisitosEnvio
{
    public static RevisionRequisitos Revisar(Predio predio, Poligono? poligono, IReadOnlyList<Documento> documentos, Parametros p)
    {
        var lista = new List<Requisito>();

        // 1. Datos básicos del predio.
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(predio.Nombre)) faltan.Add("nombre");
        if (string.IsNullOrWhiteSpace(predio.Departamento)) faltan.Add("departamento");
        if (string.IsNullOrWhiteSpace(predio.Municipio)) faltan.Add("municipio");
        if (string.IsNullOrWhiteSpace(predio.Cultivo)) faltan.Add("cultivo");
        if (predio.SuperficieDeclaradaHa <= 0) faltan.Add("superficie declarada");
        lista.Add(new("datos", "Datos del predio", faltan.Count == 0,
            faltan.Count == 0 ? "Nombre, departamento, municipio, cultivo y superficie declarada." : $"Falta: {string.Join(", ", faltan)}.",
            "predios"));

        // 2. Ubicación: polígono o punto según la superficie declarada (RF-05).
        var exigePoligono = predio.SuperficieDeclaradaHa > p.UmbralPoligonoObligatorioHa;
        lista.Add(poligono switch
        {
            null => new("ubicacion", exigePoligono ? "Polígono del predio" : "Punto o polígono del predio", false,
                exigePoligono
                    ? $"El predio declara más de {p.UmbralPoligonoObligatorioHa:0.##} ha: carga el polígono completo."
                    : "Registra un punto dentro del predio o carga su polígono.",
                "poligonos"),
            { EsPunto: true } when exigePoligono => new("ubicacion", "Polígono del predio", false,
                $"Se registró un punto, pero con más de {p.UmbralPoligonoObligatorioHa:0.##} ha se exige el polígono completo.",
                "poligonos"),
            { EsPunto: true } => new("ubicacion", "Punto o polígono del predio", true, "Se registró un punto.", "poligonos"),
            _ => new("ubicacion", exigePoligono ? "Polígono del predio" : "Punto o polígono del predio", true,
                $"Polígono registrado (versión {poligono.Version}).", "poligonos")
        });

        // 3. Titularidad: el título del INRA se registra con el predio (número, CI del titular y superficie titulada).
        var titulo = documentos.FirstOrDefault(d => d.Tipo == TipoDocumento.TituloPropiedad);
        var faltaTitulo = new List<string>();
        if (titulo is null || string.IsNullOrWhiteSpace(titulo.Numero)) faltaTitulo.Add("número de título");
        if (string.IsNullOrWhiteSpace(titulo?.TitularCi)) faltaTitulo.Add("CI del titular");
        if (titulo?.SuperficieHa is null) faltaTitulo.Add("superficie titulada");
        lista.Add(new("titulo", "Título de propiedad (INRA)", faltaTitulo.Count == 0,
            faltaTitulo.Count == 0
                ? $"N.º {titulo!.Numero}, titular CI {titulo.TitularCi}, {titulo.SuperficieHa:0.##} ha."
                : $"Completa en los datos del predio: {string.Join(", ", faltaTitulo)}.",
            "predios"));

        // 4. Desmonte declarado: decide si el predio entra en la exención de la Ley 741.
        var desmonte = predio.DesmonteDeclaradoHa;
        lista.Add(new("desmonte", "Desmonte declarado", desmonte is not null,
            desmonte is null
                ? "Escribe las hectáreas de desmonte en los datos del predio (0 si no hubo desmonte)."
                : $"{desmonte:0.##} ha.",
            "predios"));

        // 5 y 6. Autorizaciones de la ABT, solo si el predio no entra en la Ley 741. Se usa la misma regla que la capa
        // de uso de suelo: el área del polígono si existe, o la superficie declarada.
        if (desmonte is double d)
        {
            var superficie = poligono is { EsPunto: false } && RevisionGeometrica.Revisar(poligono.Vertices) is null
                ? Geometrias.AreaHectareas(poligono.Vertices)
                : predio.SuperficieDeclaradaHa;
            var exento = superficie <= p.Ley741PredioMaximoHa || d <= p.Ley741DesmonteMaximoHa;

            if (!exento)
            {
                // Cuenta solo un documento con número y superficie: un registro vacío no cumple el requisito.
                var pop = documentos.Any(x => x.Tipo == TipoDocumento.PlanOrdenamientoPredial && Completo(x));
                lista.Add(new("pop", "Plan de ordenamiento predial (ABT)", pop,
                    pop ? "Registrado con número y superficie autorizada." : $"El predio supera los límites de la Ley 741 ({p.Ley741PredioMaximoHa:0.##} ha de predio " +
                                          $"y {p.Ley741DesmonteMaximoHa:0.##} ha de desmonte): registra el POP con su número y superficie autorizada.",
                    "documentos"));

                if (d > 0)
                {
                    var plan = documentos.Any(x => x.Tipo == TipoDocumento.PlanDesmonte && Completo(x));
                    lista.Add(new("plan-desmonte", "Plan de desmonte (ABT)", plan,
                        plan ? "Registrado con número y superficie autorizada." : $"Se declaran {d:0.##} ha de desmonte: registra el plan de desmonte con su número y superficie autorizada.",
                        "documentos"));
                }
            }
        }

        return new RevisionRequisitos(predio.Id, lista.All(r => r.Cumplido), lista);
    }

    private static bool Completo(Documento d) => !string.IsNullOrWhiteSpace(d.Numero) && d.SuperficieHa is > 0;
}
