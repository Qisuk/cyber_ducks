namespace Store.Web.Services;

/// <summary>
/// Rewrites the OIDC backchannel host from Keycloak's public URL (localhost:8091, what the browser
/// and the token issuer use) to the internal container address (idp:8080). This lets the issuer in
/// tokens stay browser-consistent while store-web, inside Docker, can still reach the IdP for
/// metadata and token exchange.
/// </summary>
public sealed class KeycloakBackchannelHandler(string publicHost, string internalHost) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri is { } uri && $"{uri.Host}:{uri.Port}" == publicHost)
        {
            var builder = new UriBuilder(uri)
            {
                Host = internalHost.Split(':')[0],
                Port = int.Parse(internalHost.Split(':')[1]),
            };
            request.RequestUri = builder.Uri;
        }
        return base.SendAsync(request, ct);
    }
}
