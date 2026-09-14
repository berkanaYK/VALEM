namespace VALE.Contracts;

public sealed record VehicleCatalogBrand(string Name, IReadOnlyList<string> Models);

public static class VehicleCatalog
{
    private static readonly StringComparer Alphabetical = StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true);
    public static IReadOnlyList<VehicleCatalogBrand> Brands { get; } =
    new List<VehicleCatalogBrand>
    {
        new("Alfa Romeo", ["Giulia", "Giulietta", "Stelvio", "Tonale"]),
        new("Audi", ["A3", "A4", "A5", "A6", "Q2", "Q3", "Q5", "Q7", "e-tron"]),
        new("BMW", ["1 Serisi", "2 Serisi", "3 Serisi", "4 Serisi", "5 Serisi", "X1", "X2", "X3", "X5", "i4", "iX"]),
        new("Citroën", ["C3", "C4", "C4 X", "C5 Aircross", "Berlingo"]),
        new("Chevrolet", ["Aveo", "Captiva", "Cruze", "Spark", "Trax"]),
        new("CUPRA", ["Ateca", "Born", "Formentor", "Leon"]),
        new("Dacia", ["Sandero", "Sandero Stepway", "Duster", "Jogger"]),
        new("Fiat", ["Egea", "Egea Cross", "500", "500X", "Fiorino", "Doblo"]),
        new("Ford", ["Fiesta", "Focus", "Puma", "Kuga", "Tourneo Courier", "Ranger"]),
        new("Honda", ["Civic", "City", "HR-V", "CR-V", "Jazz"]),
        new("Hyundai", ["i10", "i20", "Bayon", "Elantra", "Tucson", "Kona", "IONIQ 5", "IONIQ 6"]),
        new("Jeep", ["Avenger", "Compass", "Renegade", "Wrangler"]),
        new("Kia", ["Picanto", "Rio", "Stonic", "Ceed", "XCeed", "Sportage", "Niro", "EV3", "EV6"]),
        new("Land Rover", ["Defender", "Discovery", "Discovery Sport", "Range Rover", "Range Rover Evoque", "Range Rover Sport"]),
        new("Mazda", ["Mazda 2", "Mazda 3", "CX-3", "CX-5", "CX-30", "MX-5"]),
        new("Mercedes-Benz", ["A Serisi", "B Serisi", "C Serisi", "E Serisi", "CLA", "GLA", "GLC", "GLE", "EQA", "EQB", "EQE"]),
        new("MG", ["MG3", "MG4", "HS", "Marvel R", "ZS"]),
        new("MINI", ["Cooper", "Countryman", "Clubman", "Aceman"]),
        new("Mitsubishi", ["ASX", "Colt", "Eclipse Cross", "L200", "Outlander"]),
        new("Nissan", ["Micra", "Juke", "Qashqai", "X-Trail", "Ariya"]),
        new("Opel", ["Corsa", "Astra", "Mokka", "Crossland", "Grandland"]),
        new("Peugeot", ["208", "308", "408", "2008", "3008", "5008", "Rifter"]),
        new("Porsche", ["718", "911", "Cayenne", "Macan", "Panamera", "Taycan"]),
        new("Renault", ["Clio", "Megane", "Megane E-Tech", "Captur", "Austral", "Rafale", "Kangoo"]),
        new("SEAT", ["Ibiza", "Leon", "Arona", "Ateca"]),
        new("Škoda", ["Fabia", "Scala", "Octavia", "Superb", "Kamiq", "Karoq", "Kodiaq", "Enyaq"]),
        new("Subaru", ["BRZ", "Crosstrek", "Forester", "Impreza", "Outback", "Solterra"]),
        new("Suzuki", ["S-Cross", "Swift", "Vitara", "Jimny"]),
        new("Tesla", ["Model 3", "Model Y", "Model S", "Model X"]),
        new("TOGG", ["T10X", "T10F"]),
        new("Toyota", ["Yaris", "Corolla", "Corolla Cross", "C-HR", "RAV4", "Proace City"]),
        new("Volkswagen", ["Polo", "Golf", "Passat", "T-Cross", "Taigo", "T-Roc", "Tiguan", "ID.3", "ID.4", "ID.7"]),
        new("Volvo", ["EX30", "EX40", "XC40", "XC60", "XC90", "S60", "S90"])
    }.OrderBy(x => x.Name, Alphabetical).ToList();

    public static IReadOnlyList<string> FuelTypes { get; } = ["Benzin", "Dizel", "LPG", "Hibrit", "Plug-in Hibrit", "Elektrik"];
    public static IReadOnlyList<string> Transmissions { get; } = ["Otomatik", "Manuel", "Yarı Otomatik"];

    public static IReadOnlyList<string> ModelsFor(string? brand) =>
        Brands.FirstOrDefault(x => string.Equals(x.Name, brand, StringComparison.OrdinalIgnoreCase))?.Models
            .OrderBy(x => x, Alphabetical).ToList()
        ?? (IReadOnlyList<string>)Array.Empty<string>();
}
