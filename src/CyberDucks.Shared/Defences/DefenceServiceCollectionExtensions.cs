using System.Diagnostics;
using CyberDucks.Shared.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberDucks.Shared.Defences;

public static class DefenceServiceCollectionExtensions
{
    public const string VulnerableKey = "vulnerable";
    public const string SecureKey = "secure";

    /// <summary>
    /// Registers the flag store and its background sync, reading the 'Flags' connection string
    /// (flags_reader on range_control).
    /// </summary>
    public static IServiceCollection AddDefenceFlags(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Flags")
            ?? throw new InvalidOperationException("ConnectionStrings:Flags is not configured.");

        services.AddSingleton<DefenceFlagStore>();
        services.AddSingleton<IDefenceFlags>(sp => sp.GetRequiredService<DefenceFlagStore>());
        services.AddSingleton(new DefenceFlagOptions(connectionString));
        services.AddHostedService<DefenceFlagSync>();
        return services;
    }

    /// <summary>
    /// Registers one behaviour that has a vulnerable and a secure implementation (ADR-0011).
    /// Both are keyed services; resolving <typeparamref name="TService"/> picks one per scope (request)
    /// from the current flag, and tags the current trace with the decision.
    /// </summary>
    public static IServiceCollection AddDefence<TService, TVulnerable, TSecure>(
        this IServiceCollection services, string vulnId)
        where TService : class
        where TVulnerable : class, TService
        where TSecure : class, TService
    {
        services.AddKeyedScoped<TService, TVulnerable>(VulnerableKey);
        services.AddKeyedScoped<TService, TSecure>(SecureKey);
        services.AddScoped<TService>(sp =>
        {
            var mode = sp.GetRequiredService<IDefenceFlags>().Get(vulnId);
            // Searchable tag on the request span (e.g. in Tempo: defence.VULN-IDOR-01 = "vulnerable"),
            // plus an event recording exactly which service was resolved.
            Activity.Current?.SetTag($"defence.{vulnId}", mode.ToString().ToLowerInvariant());
            Activity.Current?.AddEvent(new ActivityEvent("defence.resolved", tags: new ActivityTagsCollection
            {
                ["defence.vuln_id"] = vulnId,
                ["defence.mode"] = mode.ToString().ToLowerInvariant(),
                ["defence.service"] = typeof(TService).Name,
            }));
            return sp.GetRequiredKeyedService<TService>(mode == DefenceMode.Secure ? SecureKey : VulnerableKey);
        });
        return services;
    }
}
