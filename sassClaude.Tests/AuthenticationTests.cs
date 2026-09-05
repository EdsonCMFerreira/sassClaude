using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using sassClaude.Models;

namespace sassClaude.Tests;

public sealed class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Dashboard_redirects_anonymous_user_to_login()
    {
        var response = await _client.GetAsync("/Dashboard");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Login_and_register_pages_are_public()
    {
        var login = await _client.GetAsync("/Account/Login");
        var register = await _client.GetAsync("/Account/Register");

        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, register.StatusCode);
    }

    [Fact]
    public async Task Password_recovery_pages_are_public()
    {
        var forgot = await _client.GetAsync("/Account/ForgotPassword");
        var invalidReset = await _client.GetAsync("/Account/ResetPassword?token=invalid");

        Assert.Equal(System.Net.HttpStatusCode.OK, forgot.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, invalidReset.StatusCode);
    }

    [Fact]
    public async Task User_api_requires_an_authenticated_session()
    {
        var response = await _client.GetAsync("/api/login");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void PasswordHasher_does_not_store_the_original_password()
    {
        var user = new Login { Username = "teste", Email = "teste@teste.com" };
        var hasher = new PasswordHasher<Login>();
        var hash = hasher.HashPassword(user, "senha-segura");

        Assert.NotEqual("senha-segura", hash);
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, hash, "senha-segura"));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, hash, "senha-incorreta"));
    }
}
