extern alias storeweb;
using storeweb::Store.Web.Services;

namespace CyberDucks.Tests;

/// <summary>
/// The login/register form post carries a hidden <c>returnUrl</c> field, so its value is
/// caller-controlled. <see cref="AccountEndpoints.ResolveLocalReturnUrl"/> must hand
/// <c>Results.LocalRedirect</c> a value it accepts — a safe local path or "/" — because
/// LocalRedirect throws (→ HTTP 500) on anything non-local. Regression test for the login
/// form 500ing whenever a non-local returnUrl was submitted.
/// </summary>
public sealed class ReturnUrlTests
{
    [Theory]
    // Missing / empty → home.
    [InlineData(null, "/")]
    [InlineData("", "/")]
    // Safe local paths are honoured verbatim.
    [InlineData("/", "/")]
    [InlineData("/account", "/account")]
    [InlineData("/orders/5", "/orders/5")]
    [InlineData("/orders?page=2", "/orders?page=2")]
    [InlineData("~/account", "~/account")]
    // Open-redirect / non-local values fall back to home instead of throwing.
    [InlineData("http://evil.example", "/")]
    [InlineData("https://evil.example/path", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("~//evil.example", "/")]
    [InlineData("~/\\evil.example", "/")]
    [InlineData("javascript:alert(1)", "/")]
    [InlineData("account", "/")]            // relative, no leading slash
    [InlineData("/account\r\nSet-Cookie: x", "/")] // control chars (header-injection shape)
    public void Resolves_to_a_safe_local_target(string? returnUrl, string expected)
    {
        Assert.Equal(expected, AccountEndpoints.ResolveLocalReturnUrl(returnUrl));
    }
}
