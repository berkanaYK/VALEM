using VALE.Contracts;
using Xunit;

namespace VALE.Api.Tests;

public sealed class VehicleCatalogTests
{
    [Fact]
    public void Brands_and_each_brand_models_are_alphabetical_and_model_requires_brand()
    {
        var comparer = StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true);
        Assert.Equal(VehicleCatalog.Brands.Select(x => x.Name).OrderBy(x => x, comparer), VehicleCatalog.Brands.Select(x => x.Name));
        foreach (var brand in VehicleCatalog.Brands)
            Assert.Equal(brand.Models.OrderBy(x => x, comparer), VehicleCatalog.ModelsFor(brand.Name));
        Assert.Empty(VehicleCatalog.ModelsFor(null));
        Assert.Empty(VehicleCatalog.ModelsFor("Bilinmeyen"));
    }
}
