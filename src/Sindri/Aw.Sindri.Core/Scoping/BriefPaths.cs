namespace Aw.Sindri.Core.Scoping;

/// <summary>Which paths a brief may never be read from (SND-SCOPING-004): the one list a file reader and its fake both apply.</summary>
public static class BriefPaths
{
    /// <summary>The most a brief may be, in bytes.</summary>
    public const long MaxBytes = 1024 * 1024;

    private static readonly string[] _deniedSegments = [".git", ".ssh", ".aws", ".gnupg", ".env", ".netrc", ".npmrc"];
    private static readonly string[] _deniedExtensions = [".pem", ".key", ".p12", ".pfx", ".keystore", ".secret", ".secrets"];
    private static readonly string[] _deniedPrefixes = [".env.", "id_rsa", "id_ed25519", "id_ecdsa", "credentials", "secrets."];

    /// <summary>Whether a relative path names an environment file, a key, a credentials file, .git or .ssh in any of its segments.</summary>
    /// <param name="relative">The path relative to its root.</param>
    /// <returns>True when it is denied.</returns>
    public static bool Denied(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        return relative.Split('/', '\\').Any(static segment =>
            _deniedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)
            || _deniedExtensions.Contains(Extension(segment), StringComparer.OrdinalIgnoreCase)
            || _deniedPrefixes.Any(prefix => segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
    }

    private static string Extension(string segment) => segment.LastIndexOf('.') is var dot and >= 0 ? segment[dot..] : string.Empty;
}
