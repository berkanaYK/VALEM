namespace VALE.Client.Services;

public static class VehicleCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> Models =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Audi"] = ["A3", "A4", "A5", "A6", "Q3", "Q5", "Q7", "e-tron"],
            ["BMW"] = ["1 Serisi", "2 Serisi", "3 Serisi", "4 Serisi", "5 Serisi", "X1", "X3", "X5", "i4"],
            ["Citroën"] = ["C3", "C4", "C4 X", "C5 Aircross", "Berlingo"],
            ["Dacia"] = ["Sandero", "Stepway", "Duster", "Jogger"],
            ["Fiat"] = ["Egea", "Egea Cross", "500", "500X", "Doblo", "Fiorino"],
            ["Ford"] = ["Focus", "Puma", "Kuga", "Ranger", "Tourneo Courier", "Transit"],
            ["Honda"] = ["Civic", "City", "HR-V", "CR-V", "Jazz"],
            ["Hyundai"] = ["i10", "i20", "Bayon", "Elantra", "Tucson", "Kona", "IONIQ 5"],
            ["Kia"] = ["Picanto", "Stonic", "Ceed", "Sportage", "Sorento", "EV3", "EV6"],
            ["Mercedes-Benz"] = ["A Serisi", "C Serisi", "E Serisi", "CLA", "GLA", "GLC", "GLE", "EQE"],
            ["Nissan"] = ["Juke", "Qashqai", "X-Trail", "Micra", "Navara"],
            ["Opel"] = ["Corsa", "Astra", "Mokka", "Crossland", "Grandland"],
            ["Peugeot"] = ["208", "308", "408", "2008", "3008", "5008", "Rifter"],
            ["Renault"] = ["Clio", "Megane", "Austral", "Captur", "Kangoo", "Rafale"],
            ["SEAT"] = ["Ibiza", "Leon", "Arona", "Ateca", "Tarraco"],
            ["Skoda"] = ["Fabia", "Scala", "Octavia", "Superb", "Kamiq", "Karoq", "Kodiaq"],
            ["Tesla"] = ["Model 3", "Model Y", "Model S", "Model X"],
            ["TOGG"] = ["T10X", "T10F"],
            ["Toyota"] = ["Corolla", "Corolla Cross", "C-HR", "Yaris", "Yaris Cross", "RAV4", "Hilux"],
            ["Volkswagen"] = ["Polo", "Golf", "Passat", "T-Roc", "Tiguan", "Taigo", "Caddy", "ID.4"],
            ["Volvo"] = ["S60", "S90", "XC40", "XC60", "XC90", "EX30"]
        };

    public static IReadOnlyList<string> Brands { get; } = Models.Keys.OrderBy(x => x).Concat(["Diğer"]).ToList();
    public static IReadOnlyList<string> FuelTypes { get; } = ["Benzin", "Dizel", "LPG", "Hibrit", "Plug-in Hibrit", "Elektrik"];
    public static IReadOnlyList<string> Transmissions { get; } = ["Otomatik", "Manuel", "Yarı Otomatik"];

    public static IReadOnlyList<string> ModelsFor(string? brand) =>
        !string.IsNullOrWhiteSpace(brand) && Models.TryGetValue(brand, out var models)
            ? models
            : [];
}
