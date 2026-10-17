using System.Text.Json;

namespace Store.Web.Services;

public sealed record BasketLine(int ProductId, string Name, string Sku, decimal UnitPrice, int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>
/// A per-browser basket kept in a cookie. Being client-side, the prices in it are attacker-editable,
/// which is exactly what the VULN-LOGIC-01 checkout then does (or doesn't) trust.
/// </summary>
public sealed class BasketService(IHttpContextAccessor accessor)
{
    private const string CookieName = "duck_basket";

    public IReadOnlyList<BasketLine> GetLines()
    {
        var cookie = accessor.HttpContext?.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(cookie))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<BasketLine>>(cookie) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public int Count => GetLines().Sum(l => l.Quantity);

    public decimal Total => GetLines().Sum(l => l.LineTotal);

    public void Add(BasketLine line)
    {
        var lines = GetLines().ToList();
        var existing = lines.FindIndex(l => l.ProductId == line.ProductId);
        if (existing >= 0)
        {
            lines[existing] = lines[existing] with { Quantity = lines[existing].Quantity + line.Quantity };
        }
        else
        {
            lines.Add(line);
        }
        Save(lines);
    }

    public void SetQuantity(int productId, int quantity)
    {
        var lines = GetLines().Where(l => l.ProductId != productId).ToList();
        if (quantity > 0)
        {
            var current = GetLines().FirstOrDefault(l => l.ProductId == productId);
            if (current is not null)
            {
                lines.Add(current with { Quantity = quantity });
            }
        }
        Save(lines);
    }

    public void Clear() => accessor.HttpContext?.Response.Cookies.Delete(CookieName);

    private void Save(IReadOnlyList<BasketLine> lines)
    {
        var http = accessor.HttpContext;
        if (http is null)
        {
            return;
        }
        if (lines.Count == 0)
        {
            http.Response.Cookies.Delete(CookieName);
            return;
        }
        // Not HttpOnly on purpose for the range: the basket (and its prices) is client-visible and
        // editable, which makes the VULN-LOGIC-01 price-tampering demo concrete.
        http.Response.Cookies.Append(CookieName, JsonSerializer.Serialize(lines), new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(7),
        });
    }
}
