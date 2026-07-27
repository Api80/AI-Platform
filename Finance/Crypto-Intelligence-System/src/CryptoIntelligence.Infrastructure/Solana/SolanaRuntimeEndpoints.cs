using CryptoIntelligence.Application.Configuration;

namespace CryptoIntelligence.Infrastructure.Solana;

public sealed record SolanaRuntimeEndpoints(
    Uri WebSocket,
    Uri PrimaryHttp,
    Uri? FallbackHttp)
{
    public static SolanaRuntimeEndpoints? Create(
        MvpConfiguration configuration,
        string? webSocketUrl,
        string? primaryHttpUrl,
        string? fallbackHttpUrl)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var hasWebSocket = !string.IsNullOrWhiteSpace(webSocketUrl);
        var hasPrimary = !string.IsNullOrWhiteSpace(primaryHttpUrl);
        var hasFallback = !string.IsNullOrWhiteSpace(fallbackHttpUrl);
        if (!hasWebSocket && !hasPrimary && !hasFallback)
        {
            if (configuration.FormalRun)
            {
                throw new InvalidOperationException(
                    "Formal runs require WebSocket, primary HTTP and fallback HTTP RPC endpoints.");
            }

            return null;
        }

        if (!hasWebSocket || !hasPrimary)
        {
            throw new InvalidOperationException(
                "SOLANA_RPC_WS_URL and SOLANA_RPC_HTTP_URL must be supplied together.");
        }

        if (configuration.FormalRun && !hasFallback)
        {
            throw new InvalidOperationException(
                "Formal runs require SOLANA_RPC_FALLBACK_HTTP_URL.");
        }

        var webSocket = RequireEndpoint(
            webSocketUrl!,
            "SOLANA_RPC_WS_URL",
            "ws",
            "wss");
        var primary = RequireEndpoint(
            primaryHttpUrl!,
            "SOLANA_RPC_HTTP_URL",
            Uri.UriSchemeHttp,
            Uri.UriSchemeHttps);
        var fallback = hasFallback
            ? RequireEndpoint(
                fallbackHttpUrl!,
                "SOLANA_RPC_FALLBACK_HTTP_URL",
                Uri.UriSchemeHttp,
                Uri.UriSchemeHttps)
            : null;
        if (fallback is not null &&
            Uri.Compare(
                primary,
                fallback,
                UriComponents.HttpRequestUrl,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0)
        {
            throw new InvalidOperationException(
                "Primary and fallback HTTP RPC endpoints must be distinct.");
        }

        return new SolanaRuntimeEndpoints(webSocket, primary, fallback);
    }

    private static Uri RequireEndpoint(
        string value,
        string variableName,
        params string[] allowedSchemes)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
            !allowedSchemes.Contains(
                endpoint.Scheme,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{variableName} must be an absolute " +
                $"{string.Join('/', allowedSchemes)} URL.");
        }

        return endpoint;
    }
}
