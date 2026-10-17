using System.Security.Claims;
using CyberDucks.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Store.Web.Services;

/// <summary>
/// Plain form-post endpoints for sign-in/out. These need to write the auth cookie (an HttpContext
/// response), so they're minimal-API endpoints rather than Blazor components. The Login/Register
/// pages post ordinary HTML forms here.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccount(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/account").DisableAntiforgery();

        account.MapPost("/login", async (
            HttpContext http, StoreApiClient api,
            [FromForm] string email, [FromForm] string password, [FromForm] string? returnUrl) =>
        {
            var (result, error) = await api.LoginAsync(new LoginRequest(email, password));
            return await CompleteAsync(http, result, error, email, returnUrl, "/login");
        });

        account.MapPost("/register", async (
            HttpContext http, StoreApiClient api,
            [FromForm] string email, [FromForm] string displayName, [FromForm] string password) =>
        {
            var (result, error) = await api.RegisterAsync(new RegisterRequest(email, displayName, password));
            return await CompleteAsync(http, result, error, email, null, "/register");
        });

        account.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });

        // Profile edit. The API decides whether a role change is honoured (VULN-MASS-01); here we
        // re-sign-in with the re-issued token so any change is reflected immediately.
        account.MapPost("/profile", async (
            HttpContext http, StoreApiClient api, CsrfGuard csrf,
            [FromForm] string displayName, [FromForm] string? role) =>
        {
            if (!await csrf.ValidateAsync(http))
            {
                return Results.BadRequest("Invalid anti-forgery token.");
            }
            var result = await api.UpdateProfileAsync(new UpdateProfileRequest(displayName, role));
            if (result is not null)
            {
                await SignInAsync(http, result);
            }
            return Results.LocalRedirect("/account");
        }).RequireAuthorization();

        // Post a review, then back to the product.
        app.MapPost("/ducks/{id:int}/review", async (
            int id, HttpContext http, StoreApiClient api, CsrfGuard csrf,
            [FromForm] int rating, [FromForm] string body) =>
        {
            if (!await csrf.ValidateAsync(http))
            {
                return Results.BadRequest("Invalid anti-forgery token.");
            }
            await api.PostReviewAsync(id, new PostReviewRequest(rating, body));
            return Results.LocalRedirect($"/ducks/{id}");
        }).DisableAntiforgery().RequireAuthorization();

        // --- Basket (anonymous allowed; checkout requires login) ---
        // Add to basket: the price is captured from the catalogue server-side at add time.
        app.MapPost("/basket/add", async (
            [FromForm] int productId, [FromForm] int quantity, StoreApiClient api, BasketService basket) =>
        {
            var product = (await api.GetProductsAsync()).FirstOrDefault(p => p.Id == productId);
            if (product is not null)
            {
                basket.Add(new BasketLine(product.Id, product.Name, product.Sku, product.Price, Math.Max(1, quantity)));
            }
            return Results.LocalRedirect("/basket");
        }).DisableAntiforgery();

        app.MapPost("/basket/update", ([FromForm] int productId, [FromForm] int quantity, BasketService basket) =>
        {
            basket.SetQuantity(productId, quantity);
            return Results.LocalRedirect("/basket");
        }).DisableAntiforgery();

        app.MapPost("/basket/checkout", async (
            HttpContext http, StoreApiClient api, BasketService basket,
            [FromForm] string shippingAddress, [FromForm] string? notes) =>
        {
            var lines = basket.GetLines();
            if (lines.Count == 0)
            {
                return Results.LocalRedirect("/basket");
            }
            // The basket's prices (client-editable cookie) are sent as-is; VULN-LOGIC-01 on the API
            // decides whether to trust them.
            var request = new PlaceOrderRequest(shippingAddress, notes,
                lines.Select(l => new PlaceOrderItem(l.ProductId, l.Quantity, l.UnitPrice)).ToList());
            var (result, error) = await api.PlaceOrderAsync(request);
            if (result is null)
            {
                return Results.LocalRedirect($"/checkout?error={Uri.EscapeDataString(error ?? "Order failed.")}");
            }
            basket.Clear();
            return Results.LocalRedirect($"/orders/{result.OrderId}?placed=true");
        }).DisableAntiforgery().RequireAuthorization();

        return account;
    }

    private static async Task<IResult> CompleteAsync(
        HttpContext http, AuthResult? result, string? error, string email, string? returnUrl, string formPath)
    {
        if (result is null)
        {
            // Re-show the form with the error (and the email, so it isn't retyped).
            var query = QueryString.Create(new Dictionary<string, string?>
            {
                ["error"] = error,
                ["email"] = email,
            });
            return Results.Redirect(formPath + query);
        }

        await SignInAsync(http, result);
        return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    private static async Task SignInAsync(HttpContext http, AuthResult result)
    {
        var claims = new List<Claim>
        {
            new("sub", result.User.Id.ToString()),
            new("email", result.User.Email),
            new("name", result.User.DisplayName),
            new(ClaimTypes.Role, result.User.Role),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, "name", ClaimTypes.Role);

        var properties = new AuthenticationProperties { ExpiresUtc = result.ExpiresAt };
        properties.StoreTokens([new AuthenticationToken { Name = "jwt", Value = result.Token }]);

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
    }
}
