using System.Net.Mail;

namespace Store.Api.Email;

/// <summary>Sends mail to the range's Mailpit catcher (flow F15). Fake mail only; never leaves the range.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> log) : IEmailSender
{
    private readonly string _host = config["Smtp:Host"] ?? "mailpit";
    private readonly int _port = int.TryParse(config["Smtp:Port"], out var p) ? p : 1025;
    private readonly string _from = config["Smtp:From"] ?? "no-reply@duckstore.test";

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_host, _port);
        using var message = new MailMessage(_from, to, subject, body);
        await client.SendMailAsync(message, ct);
        log.LogInformation("Sent email to {To}: {Subject}", to, subject);
    }
}
