using FinSight.Api.IntegrationTests.Infrastructure;
using FinSight.Api.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinSight.Api.IntegrationTests.Features.Auth;

[Collection("Integration")]
public sealed class AuthEndpointsTests(FinSightWebAppFactory factory)
    : IClassFixture<DatabaseResetFixture>, IClassFixture<FinSightWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_WithValidData_Returns201WithAccessToken()
    {
        // Arrange
        var request = TestDataBuilder.RegisterRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("email").GetString().Should().NotBeNullOrEmpty();

        // Refresh token should be in HTTP-only cookie
        response.Headers.Should().ContainKey("Set-Cookie");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409Conflict()
    {
        // Arrange
        var email = "duplicate@finsight.dev";
        var request = TestDataBuilder.RegisterRequest(email);
        await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Act — register again with same email
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns422()
    {
        // Arrange
        var request = new
        {
            email = "weak@finsight.dev",
            password = "weak",  // fails validation rules
            firstName = "Weak",
            lastName = "Password"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        // Arrange — register first
        var email = "login-test@finsight.dev";
        var password = "Test@Password1!";
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            TestDataBuilder.RegisterRequest(email));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            TestDataBuilder.RegisterRequest("wrongpw@finsight.dev"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "wrongpw@finsight.dev",
            password = "WrongPassword1!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
