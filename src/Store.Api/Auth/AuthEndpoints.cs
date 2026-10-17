using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using CyberDucks.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Data;

namespace Store.Api.Auth;

public static class AuthEndpoints
{
    // Counts login outcomes by result; the Phase 8 detections build on this baseline.
    private static readonly Meter Meter = new("CyberDucks.Auth");
    private static readonly Counter<long> LoginAttempts =
        Meter.CreateCounter<long>("cyberducks.login.attempts", description: "Login attempts by result.");

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/register", RegisterAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapGet("/me", Me).RequireAuthorization();

        return auth;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, StoreDbContext db, JwtIssuer jwt, ILogger<Program> log, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (email.Length == 0 || !email.Contains('@') || request.Password.Length < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["A valid email and a password are required."],
            });
        }

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return Results.Conflict(new { message = "An account with that email already exists." });
        }

        // New accounts are always customers: role is never taken from the request (avoids the
        // mass-assignment path that VULN-MASS-01 will demonstrate on profile updates).
        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim() is { Length: > 0 } name ? name : email,
            Role = "customer",
            // Store BOTH forms so VULN-PWD-01 can switch at runtime (ADR-0011).
            PasswordHashLegacy = PasswordHashing.LegacyHash(request.Password),
            PasswordHash = PasswordHashing.SecureHash(request.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Registered new customer {UserId}", user.Id);

        return IssueToken(jwt, user);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext http,
        StoreDbContext db,
        IPasswordVerifier verifier,
        ILoginGuard guard,
        JwtIssuer jwt,
        ILogger<Program> log,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var clientIp = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        var block = await guard.CheckAsync(user, clientIp, ct);
        if (block != LoginBlock.None)
        {
            Record("blocked", email, clientIp, log);
            // Always generic: a blocked attempt must not confirm the account exists.
            return Results.Json(new { message = "Invalid email or password." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user is null || !verifier.Verify(user, request.Password))
        {
            await guard.RecordFailureAsync(user, clientIp, ct);
            Record(user is null ? "unknown-user" : "bad-password", email, clientIp, log);

            var message = guard.RevealsReason
                ? user is null ? "No account with that email." : "Incorrect password."
                : "Invalid email or password.";
            return Results.Json(new { message }, statusCode: StatusCodes.Status401Unauthorized);
        }

        await guard.RecordSuccessAsync(user, ct);
        Record("success", email, clientIp, log, user.Id);
        return IssueToken(jwt, user);
    }

    private static IResult Me(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null
            ? Results.Unauthorized()
            : Results.Ok(new UserInfo(
                int.Parse(id),
                principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email) ?? "",
                principal.FindFirstValue("name") ?? principal.Identity?.Name ?? "",
                principal.FindFirstValue(JwtIssuer.RoleClaim) ?? principal.FindFirstValue(ClaimTypes.Role) ?? "customer"));
    }

    private static IResult IssueToken(JwtIssuer jwt, User user)
    {
        var (token, expires) = jwt.Issue(user);
        return Results.Ok(new AuthResult(token, expires, new UserInfo(user.Id, user.Email, user.DisplayName, user.Role)));
    }

    private static void Record(string result, string email, string clientIp, ILogger log, int? userId = null)
    {
        LoginAttempts.Add(1, new KeyValuePair<string, object?>("result", result));
        Activity.Current?.SetTag("auth.result", result);
        // Never log the password. Email is an identifier, not a secret, and is useful for investigation.
        log.LogInformation("Login {Result} for {Email} from {ClientIp} (user {UserId})",
            result, email, clientIp, userId);
    }
}
