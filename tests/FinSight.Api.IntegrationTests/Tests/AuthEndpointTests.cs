using System.Net;
using System.Net.Http.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;

namespace FinSight.Api.IntegrationTests.Tests;

[Collection("Integration")]
public sealed class AuthEndpointTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_WithValidData_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = $"reg-{Guid.NewGuid():N}@test.dev",
            Password = "SecurePass1!",
            FirstName = "Jane",
            LastName = "Doe"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("accessToken");
        body!["accessToken"].ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.dev";
        var payload = new { Email = email, Password = "SecurePass1!", FirstName = "A", LastName = "B" };

        await _client.PostAsJsonAsync("/api/v1/auth/register", payload);
        var second = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = "weak@test.dev",
            Password = "weak",       // fails: no uppercase, number, special char, too short
            FirstName = "A",
            LastName = "B"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        // Arrange — register first
        var email = $"login-{Guid.NewGuid():N}@test.dev";
        const string password = "LoginPass1!";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FirstName = "A", LastName = "B"
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = password
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("accessToken");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = $"wrong-{Guid.NewGuid():N}@test.dev";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = "CorrectPass1!", FirstName = "A", LastName = "B"
        });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email, Password = "WrongPass1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
