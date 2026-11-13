namespace FlowOps.Api;

internal static class AuthDeployment
{
    public static void Validate(Uri webOrigin, string authority, string? keyPath)
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) ||
            !IsHttps(issuer) || !IsHttps(webOrigin) || webOrigin.AbsolutePath != "/")
            throw new InvalidOperationException("Production authentication requires an HTTPS origin and an HTTPS issuer.");
        if (string.IsNullOrWhiteSpace(keyPath) || !Path.IsPathFullyQualified(keyPath) || !Directory.Exists(keyPath))
            throw new InvalidOperationException("Mount an existing absolute Auth:DataProtectionKeyPath in production.");
    }

    private static bool IsHttps(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host) &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
