using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace Store.Web.Services;

/// <summary>One turn on the chat page. <see cref="ToolsUsed"/> is shown under assistant replies.</summary>
public sealed record ChatTurn(string Role, string Content, IReadOnlyList<string> ToolsUsed);

/// <summary>
/// The conversation so far, held server-side in memory (lost on restart, which is fine for a
/// range). The browser only holds a random conversation id, and the cache key also includes the
/// user id, so one customer can't read or forge another's history — or forge earlier "assistant"
/// turns of their own. Capped so the prompt stays inside the model's small context window.
/// </summary>
public sealed class ChatHistory(IMemoryCache cache, IHttpContextAccessor accessor)
{
    private const string CookieName = "duck_chat";
    private const int MaxTurns = 10;

    public IReadOnlyList<ChatTurn> Get() =>
        Key(create: false) is { } key && cache.TryGetValue(key, out List<ChatTurn>? turns) ? turns! : [];

    public void Append(params ChatTurn[] newTurns)
    {
        var key = Key(create: true)!;
        var turns = Get().Concat(newTurns).TakeLast(MaxTurns).ToList();
        cache.Set(key, turns, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(2) });
    }

    public void Clear()
    {
        if (Key(create: false) is { } key)
        {
            cache.Remove(key);
        }
        accessor.HttpContext?.Response.Cookies.Delete(CookieName);
    }

    private string? Key(bool create)
    {
        var http = accessor.HttpContext!;
        var userId = http.User.FindFirstValue("sub");
        var id = http.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(id))
        {
            if (!create)
            {
                return null;
            }
            id = Guid.NewGuid().ToString("N");
            http.Response.Cookies.Append(CookieName, id, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
        }
        return $"chat:{userId}:{id}";
    }
}
