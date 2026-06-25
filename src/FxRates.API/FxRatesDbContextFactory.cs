using FxRates.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FxRates.API;

// Used only by the EF tools (dotnet ef migrations / database update). Lets the
// design-time context resolve without booting the web host — so the startup
// Database.Migrate() never runs during migration generation. The connection string
// is a placeholder; migrations don't connect to a live database.
public class FxRatesDbContextFactory : IDesignTimeDbContextFactory<FxRatesDbContext>
{
    public FxRatesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FxRatesDbContext>()
            .UseNpgsql("Host=localhost;Database=fxrates_design;Username=postgres;Password=postgres")
            .Options;
        return new FxRatesDbContext(options);
    }
}
