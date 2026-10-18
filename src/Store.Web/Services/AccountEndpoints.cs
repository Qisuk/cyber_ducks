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

        // Start a social login: challenge the chosen OIDC provider (google / meta).
        account.MapGet("/external/{provider}", (string provider) =>
        {
            if (provider is not ("google" or "meta"))
            {
                return Results.NotFound();
            }
            var props = new AuthenticationProperties { RedirectUri = $"/account/social-complete?provider={provider}" };
            return Results.Challenge(props, [provider]);
        }).AllowAnonymous();

        // OIDC succeeded: turn the external identity into a local session via store-api.
        account.MapGet("/social-complete", async (string provider, HttpContext http, StoreApiClient api) =>
        {
            var external = await http.AuthenticateAsync("ExternalCookie");
            await http.SignOutAsync("ExternalCookie");
            if (!external.Succeeded)
            {
                return Results.Redirect("/login?error=Social+login+failed.");
            }

            var p = external.Principal!;
            var request = new SocialLoginRequest(
                provider,
                p.FindFirstValue("sub") ?? p.FindFirstValue(ClaimTypes.NameIdentifier)!,
                p.FindFirstValue("email") ?? p.FindFirstValue(ClaimTypes.Email),
                p.FindFirstValue("email_verified") == "true",
                p.FindFirstValue("name") ?? p.Identity?.Name ?? "Duck fan");

            var (result, error) = await api.SocialLoginAsync(request);
            if (result is null)
            {
                return Results.Redirect($"/login?error={Uri.EscapeDataString(error ?? "Social login failed.")}");
            }
            await SignInAsync(http, result);
            return Results.LocalRedirect("/");
        }).AllowAnonymous();

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

        // Cancel an order, then back to it.
        app.MapPost("/orders/{id:int}/cancel", async (int id, StoreApiClient api) =>
        {
            await api.CancelOrderAsync(id);
            return Results.LocalRedirect($"/orders/{id}");
        }).DisableAntiforgery().RequireAuthorization();

        // Staff: ship an order, then back to the queue.
        app.MapPost("/staff/orders/{id:int}/ship", async (int id, StoreApiClient api) =>
        {
            await api.ShipOrderAsync(id);
            return Results.LocalRedirect("/staff");
        }).DisableAntiforgery().RequireAuthorization();

        // Pay an invoice, then back to the order.
        app.MapPost("/orders/{orderId:int}/pay", async ([FromRoute] int orderId, [FromForm] int invoiceId, StoreApiClient api) =>
        {
            await api.PayInvoiceAsync(invoiceId);
            // The webhook marks it paid server-side; a brief wait lets the status settle before reload.
            await Task.Delay(400);
            return Results.LocalRedirect($"/orders/{orderId}");
        }).DisableAntiforgery().RequireAuthorization();

        // --- Ask the Duck (customer chat agent, flows F5/F9) ---
        // A plain form post that waits for the agent's reply (tens of seconds on CPU), then shows
        // the page again. Kept synchronous so every exchange is one inspectable/replayable request.
        app.MapPost("/assistant/send", async (
            HttpContext http, AgentClient agent, ChatHistory history, CsrfGuard csrf,
            [FromForm] string message) =>
        {
            if (!await csrf.ValidateAsync(http))
            {
                return Results.BadRequest("Invalid anti-forgery token.");
            }
            if (string.IsNullOrWhiteSpace(message))
            {
                return Results.LocalRedirect("/assistant");
            }

            var prior = history.Get().Select(t => new AgentChatTurn(t.Role, t.Content)).ToList();
            var (reply, error) = await agent.ChatAsync(new AgentChatRequest(message, prior), http.RequestAborted);
            if (reply is null)
            {
                return Results.LocalRedirect($"/assistant?error={Uri.EscapeDataString(error!)}");
            }
            history.Append(
                new ChatTurn("user", message, []),
                new ChatTurn("assistant", reply.Reply, reply.ToolsUsed));
            return Results.LocalRedirect("/assistant");
        }).DisableAntiforgery().RequireAuthorization();

        app.MapPost("/assistant/clear", async (HttpContext http, ChatHistory history, CsrfGuard csrf) =>
        {
            if (!await csrf.ValidateAsync(http))
            {
                return Results.BadRequest("Invalid anti-forgery token.");
            }
            history.Clear();
            return Results.LocalRedirect("/assistant");
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
