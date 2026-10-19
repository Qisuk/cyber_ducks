using System.Net;
using CyberDucks.Shared.Contracts;

namespace Store.Web.Services;

public sealed record FlagSubmitResult(bool Recognised, string Message);

/// <summary>Typed client for store-api (flow F4).</summary>
public sealed class StoreApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ProductSummary>>("api/products", ct) ?? [];

    /// <summary>Returns null when the product doesn't exist.</summary>
    public async Task<ProductDetail?> GetProductAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/products/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDetail>(ct);
    }

    public Task<PingResponse?> PingAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<PingResponse>("api/ping", ct);

    /// <summary>Submit a captured CTF flag plus a note on how it was collected ("post a flag").</summary>
    public async Task<FlagSubmitResult> SubmitFlagAsync(string flag, string? note, string? how, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/flags/submit", new { flag, note, how }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FlagSubmitResult>(ct) ?? new FlagSubmitResult(false, "Submitted.");
    }

    /// <summary>The current user from store-api, using the forwarded JWT.</summary>
    public Task<UserInfo?> GetMeAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<UserInfo>("api/auth/me", ct);

    public async Task<IReadOnlyList<OrderSummary>> GetMyOrdersAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<OrderSummary>>("api/orders", ct) ?? [];

    /// <summary>Returns null when the order doesn't exist or isn't visible to the caller.</summary>
    public async Task<OrderDetail?> GetOrderAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/orders/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDetail>(ct);
    }

    public async Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(string query, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ProductSummary>>($"api/products/search?q={Uri.EscapeDataString(query)}", ct) ?? [];

    public async Task<bool> CancelOrderAsync(int orderId, CancellationToken ct = default)
    {
        using var response = await http.PostAsync($"api/orders/{orderId}/cancel", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PayInvoiceAsync(int invoiceId, CancellationToken ct = default)
    {
        using var response = await http.PostAsync($"api/invoices/{invoiceId}/pay", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>The staff fulfilment queue. Returns null if the API refuses (not staff).</summary>
    public async Task<IReadOnlyList<StaffOrderSummary>?> GetStaffOrdersAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/staff/orders", ct);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StaffOrderSummary>>(ct);
    }

    public async Task<bool> ShipOrderAsync(int orderId, CancellationToken ct = default)
    {
        using var response = await http.PostAsync($"api/staff/orders/{orderId}/ship", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PostReviewAsync(int productId, PostReviewRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"api/products/{productId}/reviews", request, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Updates the profile. Returns the re-issued auth result (role may have changed).</summary>
    public async Task<AuthResult?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync("api/auth/profile", request, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AuthResult>(ct) : null;
    }

    /// <summary>Places an order. Returns (result, null) on success or (null, error message).</summary>
    public async Task<(PlaceOrderResult? Result, string? Error)> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/orders", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<PlaceOrderResult>(ct), null);
        }
        return (null, "Your order could not be placed. Please check your basket and try again.");
    }

    /// <summary>Attempts login. Returns the auth result, or an error message to show the user.</summary>
    public Task<(AuthResult? Result, string? Error)> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        PostAuthAsync("api/auth/login", request, ct);

    public Task<(AuthResult? Result, string? Error)> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        PostAuthAsync("api/auth/register", request, ct);

    public Task<(AuthResult? Result, string? Error)> SocialLoginAsync(SocialLoginRequest request, CancellationToken ct = default) =>
        PostAuthAsync("api/auth/social", request, ct);

    private async Task<(AuthResult?, string?)> PostAuthAsync(string path, object request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<AuthResult>(ct), null);
        }

        var problem = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
        return (null, problem?.Message ?? "Something went wrong. Please try again.");
    }

    public sealed record PingResponse(string Service, DateTime DatabaseTime);

    private sealed record ErrorResponse(string? Message);
}
