using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VALE.Api.Data;

/// <summary>
/// Allows dotnet-ef to compare the model and generate scripts without booting the API
/// or requiring a production Neon connection string. No connection is opened here.
/// </summary>
public sealed class ValeDbContextFactory : IDesignTimeDbContextFactory<ValeDbContext>
{
    public ValeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("VALE_EF_DESIGN_CONNECTION")
            ?? "Host=127.0.0.1;Database=vale_design;Username=vale_design";
        var options = new DbContextOptionsBuilder<ValeDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ValeDbContext(options);
    }
}
