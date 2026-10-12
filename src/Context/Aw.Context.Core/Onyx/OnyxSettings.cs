using Aw.Kernel;

namespace Aw.Context.Core.Onyx;

/// <summary>
/// The profile's 'context.sources.onyx' entry, checked (spec §8.8, §14 S-L2): Onyx's base URL on a loopback address,
/// the secret pointer to its API key, and the pinned release tag. Only <see cref="Create"/> makes one, so every
/// instance holds to the rules.
/// </summary>
public sealed record OnyxSettings
{
    private OnyxSettings(Uri url, string token, string version)
    {
        Url = url;
        Token = token;
        Version = version;
    }

    /// <summary>Onyx's base URL (http or https on a loopback host), ending in '/'.</summary>
    public Uri Url { get; }

    /// <summary>The secret pointer to the API key ('env:…', 'file:…', 'keychain:…', 'op:…'), never the key itself.</summary>
    public string Token { get; }

    /// <summary>The pinned Onyx release tag as the profile writes it, e.g. 'v4.9.0'.</summary>
    public string Version { get; }

    /// <summary>Checks the profile's values.</summary>
    /// <param name="url">'context.sources.onyx.url'.</param>
    /// <param name="token">'context.sources.onyx.token', a secret pointer.</param>
    /// <param name="version">'context.sources.onyx.version', a release tag.</param>
    /// <returns>The settings, SND-CONTEXT-055 for a URL or version that breaks the rules, or SND-CONTEXT-002 for no token.</returns>
    public static Result<OnyxSettings> Create(string? url, string? token, string? version) =>
        new OnyxSettings(new Uri(url ?? "http://red.invalid/"), token ?? "red", version ?? "red");

    /// <summary>Compares the version a running Onyx reports with the pinned one.</summary>
    /// <param name="reported">What 'GET /api/version' returned as 'backend_version'.</param>
    /// <returns>Success when they name the same release (a leading 'v' on either side is ignored), else SND-CONTEXT-055 naming both.</returns>
    public Result<Unit> CheckVersion(string reported) =>
        throw new NotSupportedException("red: OnyxSettings.CheckVersion");
}
