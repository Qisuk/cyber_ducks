namespace AiAgent;

/// <summary>
/// VULN-RATELIMIT-01: a simple in-memory sliding-window limiter for the chat endpoint. When the
/// defence is secure the agent caps how fast one caller can query it (slowing prompt-injection
/// brute-forcing, scripted abuse and cost/DoS); when vulnerable there is no cap. Keyed per caller
/// (session token, else client IP). Window and limit are deliberately modest for the range.
/// </summary>
public sealed class ChatRateLimiter
{
    private const int Limit = 15;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private readonly object _lock = new();
    private readonly Dictionary<string, Queue<DateTime>> _hits = new();

    /// <summary>True if this call is allowed; false if the caller is over the limit.</summary>
    public bool Allow(string key)
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            if (!_hits.TryGetValue(key, out var q))
            {
                q = new Queue<DateTime>();
                _hits[key] = q;
            }
            while (q.Count > 0 && now - q.Peek() > Window) q.Dequeue();
            if (q.Count >= Limit) return false;
            q.Enqueue(now);
            return true;
        }
    }
}
