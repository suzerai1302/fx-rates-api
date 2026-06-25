using System.Net;
using System.Net.Http.Json;

namespace FxRates.Tests;

public class AuthEndpointTests
{
    private record TokenResponse(string Token);

    [Fact]
    public async Task Register_NewUser_Returns201()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register",
            new { email = "a@example.com", password = "hunter2!" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();
        var body = new { email = "dup@example.com", password = "hunter2!" };

        await client.PostAsJsonAsync("/auth/register", body);
        var second = await client.PostAsJsonAsync("/auth/register", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register",
            new { email = "login@example.com", password = "hunter2!" });

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "login@example.com", password = "hunter2!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_BadCredentials_Returns401()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register",
            new { email = "bad@example.com", password = "hunter2!" });

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "bad@example.com", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
