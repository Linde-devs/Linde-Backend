namespace LINDE.Api.Dominio;

/// <summary>
/// Los 9 departamentos de Bolivia y sus 340 municipios (INE, división político administrativa vigente desde 2020).
/// El predio guarda el departamento y un municipio que pertenezca a él.
/// </summary>
public static class DivisionPolitica
{
    public static readonly IReadOnlyDictionary<string, string[]> Municipios = new Dictionary<string, string[]>
    {
        ["Beni"] = new[]
        {
            "Baures", "Exaltación", "Guayaramerín", "Huacaraje", "Loreto", "Magdalena", "Puerto Siles", "Reyes",
            "Riberalta", "Rurrenabaque", "San Andrés", "San Borja", "San Ignacio de Moxos", "San Javier",
            "San Joaquín", "San Ramón", "Santa Ana del Yacuma", "Santa Rosa", "Trinidad",
        },
        ["Chuquisaca"] = new[]
        {
            "Azurduy", "Camargo", "Culpina", "El Villar", "Huacareta", "Huacaya", "Icla", "Incahuasi", "Las Carreras",
            "Macharetí", "Monteagudo", "Padilla", "Poroma", "Presto", "San Lucas", "Sopachuy", "Sucre", "Tarabuco",
            "Tarvita", "Tomina", "Villa Abecia", "Villa Alcalá", "Villa Charcas", "Villa Mojocoya", "Villa Serrano",
            "Villa Vaca Guzmán (Muyupampa)", "Villa Zudáñez", "Yamparáez", "Yotala",
        },
        ["Cochabamba"] = new[]
        {
            "Aiquile", "Alalay", "Anzaldo", "Arani", "Arbieto", "Arque", "Bolívar", "Capinota", "Chimoré", "Cliza",
            "Cocapata", "Cochabamba", "Colcapirhua", "Colomi", "Cuchumuela", "Entre Ríos", "Independencia", "Mizque",
            "Morochata", "Omereque", "Pasorapa", "Pocona", "Pojo", "Puerto Villarroel", "Punata", "Quillacollo",
            "Sacaba", "Sacabamba", "San Benito", "Santiváñez", "Shinahota", "Sicaya", "Sipe Sipe", "Tacachi",
            "Tacopaya", "Tapacarí", "Tarata", "Tiquipaya", "Tiraque", "Toco", "Tolata", "Totora", "Vacas", "Vila Vila",
            "Villa Rivero", "Villa Tunari", "Vinto",
        },
        ["La Paz"] = new[]
        {
            "Achacachi", "Achocalla", "Alto Beni", "Ancoraimes", "Apolo", "Aucapata", "Ayata", "Ayo Ayo", "Batallas",
            "Cairoma", "Cajuata", "Calacoto", "Calamarca", "Callapa", "Caquiaviri", "Caranavi", "Catacora",
            "Chacarilla", "Charaña", "Charazani", "Chua Cocani", "Chulumani", "Chuma", "Collana", "Colquencha",
            "Colquiri", "Comanche", "Combaya", "Copacabana", "Coripata", "Coro Coro", "Coroico", "Curva",
            "Desaguadero", "El Alto", "Escoma", "Guanay", "Guaqui", "Huarina", "Huatajata", "Humanata", "Ichoca",
            "Inquisivi", "Irupana", "Ixiamas", "Jesús de Machaca", "La Asunta", "La Paz", "Laja", "Licoma Pampa",
            "Luribay", "Malla", "Mapiri", "Mecapaca", "Mocomoco", "Nazacara de Pacajes", "Palca", "Palos Blancos",
            "Papel Pampa", "Patacamaya", "Pelechuco", "Pucarani", "Puerto Acosta", "Puerto Carabuco", "Puerto Pérez",
            "Quiabaya", "Quime", "San Andrés de Machaca", "San Buenaventura", "San Pedro de Curahuara",
            "San Pedro de Tiquina", "Santiago de Huata", "Santiago de Machaca", "Sapahaqui", "Sica Sica", "Sorata",
            "Tacacoma", "Taraco", "Teoponte", "Tiahuanaco", "Tipuani", "Tito Yupanqui", "Umala", "Viacha",
            "Waldo Ballivián", "Yaco", "Yanacachi",
        },
        ["Oruro"] = new[]
        {
            "Antequera", "Belén de Andamarca", "Caracollo", "Carangas", "Challapata", "Chipaya", "Choquecota",
            "Coipasa", "Corque", "Cruz de Machacamarca", "Curahuara de Carangas", "El Choro", "Escara", "Esmeralda",
            "Eucaliptus", "Huachacalla", "Huanuni", "Huayllamarca", "La Rivera", "Machacamarca", "Oruro",
            "Pampa Aullagas", "Paria", "Pazña", "Poopó", "Quillacas", "Sabaya", "Salinas de Garci Mendoza",
            "Santiago de Andamarca", "Santiago de Huari", "Todos Santos", "Toledo", "Totora", "Turco",
            "Yunguyo de Litoral",
        },
        ["Pando"] = new[]
        {
            "Bella Flor", "Bolpebra", "Cobija", "Filadelfia", "Ingavi", "Nueva Esperanza", "Porvenir",
            "Puerto Gonzalo Moreno", "Puerto Rico", "San Lorenzo", "San Pedro", "Santa Rosa del Abuná",
            "Santos Mercado", "Sena", "Villa Nueva",
        },
        ["Potosí"] = new[]
        {
            "Acasio", "Arampampa", "Atocha", "Betanzos", "Caiza D", "Caripuyo", "Chaquí", "Chayanta", "Chuquihuta",
            "Ckochas", "Colcha K", "Colquechaca", "Cotagaita", "Llallagua", "Llica", "Mojinete", "Ocurí", "Pocoata",
            "Porco", "Potosí", "Puna", "Ravelo", "Sacaca", "San Agustín", "San Antonio de Esmoruco",
            "San Pablo de Lípez", "San Pedro de Buena Vista", "San Pedro de Macha", "San Pedro de Quemes", "Tacobamba",
            "Tahua", "Tinguipaya", "Tomave", "Toro Toro", "Tupiza", "Uncía", "Urmiri", "Uyuni", "Villazón", "Vitichi",
            "Yocalla",
        },
        ["Santa Cruz"] = new[]
        {
            "Ascensión de Guarayos", "Boyuibe", "Buena Vista", "Cabezas", "Camiri", "Charagua", "Colpa Bélgica",
            "Comarapa", "Concepción", "Cotoca", "Cuatro Cañadas", "Cuevo", "El Carmen Rivero Tórrez", "El Puente",
            "El Torno", "El Trigal", "Fernández Alonso", "General Saavedra", "Gutiérrez", "La Guardia", "Lagunillas",
            "Mairana", "Mineros", "Montero", "Moro Moro", "Okinawa Uno", "Pailón", "Pampagrande", "Porongo",
            "Portachuelo", "Postrervalle", "Pucará", "Puerto Quijarro", "Puerto Suárez", "Quirusillas", "Roboré",
            "Saipina", "Samaipata", "San Antonio de Lomerío", "San Carlos", "San Ignacio de Velasco", "San Javier",
            "San José de Chiquitos", "San Juan de Yapacaní", "San Julián", "San Matías", "San Miguel de Velasco",
            "San Pedro", "San Rafael de Velasco", "San Ramón", "Santa Cruz de la Sierra", "Santa Rosa del Sara",
            "Urubichá", "Vallegrande", "Warnes", "Yapacaní",
        },
        ["Tarija"] = new[]
        {
            "Bermejo", "Caraparí", "El Puente", "Entre Ríos", "Padcaya", "San Lorenzo", "Tarija", "Uriondo",
            "Villa Montes", "Yacuiba", "Yunchará",
        },
    };

    public static IEnumerable<string> Departamentos => Municipios.Keys;

    /// <summary>Devuelve el nombre oficial del departamento, sin importar mayúsculas ni tildes, o null si no existe.</summary>
    public static string? Departamento(string? nombre) =>
        Municipios.Keys.FirstOrDefault(d => Normalizar(d) == Normalizar(nombre));

    /// <summary>Devuelve el nombre oficial del municipio dentro del departamento, o null si no pertenece a él.</summary>
    public static string? Municipio(string departamento, string? nombre) =>
        Municipios.TryGetValue(departamento, out var lista) ? lista.FirstOrDefault(m => Normalizar(m) == Normalizar(nombre)) : null;

    private static string Normalizar(string? texto)
    {
        var sinTildes = new string((texto ?? "").Trim().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        return string.Join(" ", sinTildes.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
