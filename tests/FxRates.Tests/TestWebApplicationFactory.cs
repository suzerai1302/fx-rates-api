using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FxRates.Tests;

// Boots the real app in the "Testing" environment. Slice #2 will extend this to
// swap in a SQLite in-memory database (mirroring receipts-api) once persistence lands.
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
