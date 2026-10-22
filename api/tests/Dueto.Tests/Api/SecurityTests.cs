using System.Net;
using Dueto.Api.Security;
using Dueto.Tests.Infrastructure;

namespace Dueto.Tests.Api;

public class SecurityTests
{
    private const string AllowedOrigin = "https://app.dueto.example";

    // "Bad_Id" é rejeitado com 400 antes de tocar no banco, então não precisa de Postgres.
    private const string CheapRoute = "/api/wallpapers/Bad_Id";

    private static HttpRequestMessage Get(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, CheapRoute);
        request.Headers.Add("Origin", origin);
        return request;
    }

    [Fact]
    public async Task Allowed_origin_receives_cors_header()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = AllowedOrigin,
        });

        var response = await factory.CreateClient().SendAsync(Get(AllowedOrigin));

        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Unknown_origin_does_not_receive_cors_header()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = AllowedOrigin,
        });

        var response = await factory.CreateClient().SendAsync(Get("https://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task No_configured_origins_means_no_cors_header()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>());

        var response = await factory.CreateClient().SendAsync(Get(AllowedOrigin));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://app.dueto.example")]
    [InlineData("https://app.dueto.example/")]
    [InlineData("https://app.dueto.example/path")]
    [InlineData("not-a-url")]
    public void Invalid_origins_are_rejected(string origin) =>
        Assert.False(CorsPolicyOptions.IsValidOrigin(origin));

    [Theory]
    [InlineData("https://app.dueto.example")]
    [InlineData("http://localhost:4200")]
    public void Valid_origins_are_accepted(string origin) =>
        Assert.True(CorsPolicyOptions.IsValidOrigin(origin));

    [Fact]
    public void App_refuses_to_start_with_a_wildcard_origin()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "*",
        });

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public async Task Requests_over_the_limit_get_429_problem_details()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:PublicPermitLimit"] = "3",
            ["RateLimiting:PublicWindowSeconds"] = "60",
        });
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(CheapRoute)).StatusCode);
        }

        var limited = await client.GetAsync(CheapRoute);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Health_is_not_rate_limited()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:PublicPermitLimit"] = "1",
        });
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        }
    }
}
