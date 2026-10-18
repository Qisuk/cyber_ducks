using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;
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
        auth.MapPut("/profile", UpdateProfileAsync).RequireAuthorization();
        // Internal: called by store-web after a successful OIDC flow (not reachable externally).
        auth.MapPost("/social", SocialLoginAsync);
        // Password reset by email (emails land in Mailpit). VULN-RESET-01 governs token strength and
        // whether the response reveals that an account exists.
        auth.MapPost("/forgot-password", ForgotPasswordAsync);
        auth.MapPost("/reset-password", ResetPasswordAsync);

        return auth;
    }

    private static async Task<IResult> SocialLoginAsync(
        SocialLoginRequest request, StoreDbContext db, ISocialLinking linking, JwtIssuer jwt,
        ILogger<Program> log, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();

        // 1. Known identity: this provider+subject has logged in before.
        var identity = await db.UserIdentities
            .SingleOrDefaultAsync(i => i.Provider == request.Provider && i.Subject == request.Subject, ct);
        if (identity is not null)
        {
            var existing = await db.Users.FindAsync([identity.UserId], ct);
            return existing is null ? Results.Unauthorized() : IssueToken(jwt, existing);
        }

        // 2. Account linking by email (VULN-OAUTH-03).
        if (email is not null)
        {
            var byEmail = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
            if (byEmail is not null)
            {
                if (!linking.MayLinkByEmail(request.EmailVerified))
                {
                    log.LogWarning("Refused to link {Provider} login to {Email} (email not verified)", request.Provider, email);
                    return Results.Json(new { message = "That email is not verified with the provider." },
                        statusCode: StatusCodes.Status403Forbidden);
                }
                db.UserIdentities.Add(NewIdentity(byEmail.Id, request));
                await db.SaveChangesAsync(ct);
                log.LogInformation("Linked {Provider} login to existing user {UserId}", request.Provider, byEmail.Id);
                return IssueToken(jwt, byEmail);
            }
        }

        // 3. First time: create a new customer and link the identity.
        var user = new User
        {
            Email = email ?? $"{request.Provider}-{request.Subject}@social.local",
            DisplayName = request.DisplayName,
            Role = "customer",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        db.UserIdentities.Add(NewIdentity(user.Id, request));
        await db.SaveChangesAsync(ct);
        log.LogInformation("Created customer {UserId} from {Provider} login", user.Id, request.Provider);
        return IssueToken(jwt, user);
    }

    private static UserIdentity NewIdentity(int userId, SocialLoginRequest request) => new()
    {
        UserId = userId,
        Provider = request.Provider,
        Subject = request.Subject,
        Email = request.Email,
        EmailVerified = request.EmailVerified,
        LinkedAt = DateTimeOffset.UtcNow,
    };

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request, ClaimsPrincipal principal, StoreDbContext db,
        IProfileUpdate update, JwtIssuer jwt, Audit.IAuditLog audit, CancellationToken ct)
    {
        var id = int.Parse(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync([id], ct);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var oldRole = user.Role;
        // Which fields get bound is the VULN-MASS-01 decision.
        update.Apply(user, request);
        await db.SaveChangesAsync(ct);

        // A role change is security-relevant (privilege escalation via VULN-MASS-01 lands here).
        if (!string.Equals(oldRole, user.Role, StringComparison.Ordinal))
        {
            await audit.WriteAsync("role.change", user.Id, "user", user.Id.ToString(),
                new { from = oldRole, to = user.Role }, ct);
        }

        // Re-issue the token so any role change takes effect immediately (realistic for the demo).
        return IssueToken(jwt, user);
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

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request, StoreDbContext db, PasswordResetStore resets,
        Email.IEmailSender email, CyberDucks.Shared.Defences.IDefenceFlags flags,
        IConfiguration config, ILogger<Program> log, CancellationToken ct)
    {
        var address = request.Email?.Trim().ToLowerInvariant();
        var user = string.IsNullOrEmpty(address)
            ? null
            : await db.Users.SingleOrDefaultAsync(u => u.Email == address, ct);
        var secure = flags.IsSecure("VULN-RESET-01", "store-api password reset");

        if (user is not null)
        {
            var token = PasswordResetStore.NewToken(secure);
            resets.Add(token, user.Id, TimeSpan.FromMinutes(secure ? 15 : 60));
            var baseUrl = config["Store:PublicBaseUrl"] ?? "http://localhost:8080";
            var link = $"{baseUrl}/reset-password?token={token}";
            await email.SendAsync(user.Email, "Reset your Rubber Duck Emporium password",
                $"We received a request to reset your password.\n\nUse this link within "
                + $"{(secure ? "15 minutes" : "an hour")}:\n{link}\n\nIf you didn't ask for this, ignore this email.", ct);
            log.LogInformation("Password reset requested for user {UserId}", user.Id);
        }

        // VULN-RESET-01 vulnerable also leaks whether the account exists (enumeration); secure is generic.
        if (!secure && user is null)
        {
            return Results.NotFound(new { message = "No account with that email." });
        }
        return Results.Ok(new { message = "If that email has an account, a reset link has been sent." });
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request, StoreDbContext db, PasswordResetStore resets,
        ILogger<Program> log, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrEmpty(request.NewPassword))
        {
            return Results.BadRequest(new { message = "A token and a new password are required." });
        }
        if (resets.Consume(request.Token) is not { } userId)
        {
            return Results.BadRequest(new { message = "Invalid or expired reset token." });
        }
        var user = await db.Users.FindAsync([userId], ct);
        if (user is null)
        {
            return Results.BadRequest(new { message = "Invalid or expired reset token." });
        }
        user.PasswordHashLegacy = PasswordHashing.LegacyHash(request.NewPassword);
        user.PasswordHash = PasswordHashing.SecureHash(request.NewPassword);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Password reset completed for user {UserId}", user.Id);
        return Results.Ok(new { message = "Your password has been updated." });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext http,
        StoreDbContext db,
        IPasswordVerifier verifier,
        ILoginGuard guard,
        JwtIssuer jwt,
        Audit.IAuditLog audit,
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
            await audit.WriteAsync("login.blocked", user?.Id, "user", null, new { email, clientIp }, ct);
            // Always generic: a blocked attempt must not confirm the account exists.
            return Results.Json(new { message = "Invalid email or password." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (user is null || !verifier.Verify(user, request.Password))
        {
            await guard.RecordFailureAsync(user, clientIp, ct);
            Record(user is null ? "unknown-user" : "bad-password", email, clientIp, log);
            await audit.WriteAsync("login.failure", user?.Id, "user", null,
                new { email, clientIp, reason = user is null ? "unknown-user" : "bad-password" }, ct);

            var message = guard.RevealsReason
                ? user is null ? "No account with that email." : "Incorrect password."
                : "Invalid email or password.";
            return Results.Json(new { message }, statusCode: StatusCodes.Status401Unauthorized);
        }

        await guard.RecordSuccessAsync(user, ct);
        Record("success", email, clientIp, log, user.Id);
        await audit.WriteAsync("login.success", user.Id, "user", user.Id.ToString(), new { email, clientIp }, ct);
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
