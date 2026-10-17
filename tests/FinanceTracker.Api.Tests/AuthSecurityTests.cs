using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceTracker.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceTracker.Api.Tests;

public class AuthSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthSecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DevLogin_InProductionEnvironment_ReturnsNotFound()
    {
        var prodFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        });

        var client = prodFactory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/dev-login", new DevLoginRequest("partner-1"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AccountsEndpoint_WithDevHeaders_InProductionEnvironment_ReturnsUnauthorized()
    {
        var prodFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        });

        var client = prodFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");
        client.DefaultRequestHeaders.Add("X-Household-Id", "11111111-1111-1111-1111-111111111111");

        var response = await client.GetAsync("/api/accounts");

        // Must be 401 because DevAuth is completely excluded from the DI container in Production
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccountsEndpoint_WithDevCookie_InProductionEnvironment_ReturnsUnauthorized()
    {
        var prodFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        });

        var client = prodFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "dev_user_id=partner-1");

        var response = await client.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DevLogin_WithUnknownUser_InDevelopment_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/dev-login", new DevLoginRequest("intruder-hacker"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CurrentUser_CannotSpoofHouseholdId_ViaHeader()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");
        // Attacker attempts to claim they belong to a different household
        var maliciousHouseholdId = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("X-Household-Id", maliciousHouseholdId);

        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var returnedHousehold = body.GetProperty("householdId").GetString();

        // Must match the fixed household ID, NOT the header-supplied malicious ID
        returnedHousehold.Should().Be("11111111-1111-1111-1111-111111111111");
        returnedHousehold.Should().NotBe(maliciousHouseholdId);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk_AndSetsAuthCookie()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("partner1@household.local", "Password123!");

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Set-Cookie");
        var setCookie = response.Headers.GetValues("Set-Cookie").First();
        setCookie.Should().Contain("ft_auth=");
        setCookie.Should().Contain("httponly");
        setCookie.ToLowerInvariant().Should().Contain("samesite=strict");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("partner1@household.local", "WrongPassword!");

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WhenUnauthenticated_ReturnsIsAuthenticatedFalse()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Logout_InDevelopment_ClearsDevUserCookie()
    {
        var client = _factory.CreateClient();
        // First log in with dev-login
        var loginResponse = await client.PostAsJsonAsync("/api/auth/dev-login", new DevLoginRequest("partner-1"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then log out
        var logoutResponse = await client.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Check that dev_user_id was expired / deleted
        logoutResponse.Headers.Should().ContainKey("Set-Cookie");
        var cookies = logoutResponse.Headers.GetValues("Set-Cookie");
        cookies.Should().Contain(c => c.Contains("dev_user_id="));
    }
}

