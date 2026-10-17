using CyberDucks.Shared.Defences;

namespace Store.Api;

/// <summary>
/// Turns unhandled exceptions into a JSON response. VULN-INFO-01 decides how much it reveals:
/// vulnerable leaks the message, type and stack trace (helps an attacker, threat TH5/A9);
/// secure returns a generic message. The exception is always logged server-side either way.
/// </summary>
public sealed class ErrorHandlingMiddleware(RequestDelegate next, IDefenceFlags flags, ILogger<ErrorHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            log.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            if (flags.IsSecure("VULN-INFO-01"))
            {
                await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
            }
            else
            {
                await context.Response.WriteAsJsonAsync(new
                {
                    message = ex.Message,
                    type = ex.GetType().FullName,
                    stackTrace = ex.StackTrace,
                });
            }
        }
    }
}
