var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so the test project's WebApplicationFactory<Program> can boot the real app.
public partial class Program { }
