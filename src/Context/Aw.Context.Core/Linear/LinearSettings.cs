using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>
/// The profile's 'context.sources.linear' entry, checked (spec §8.8, §14 S-L2, D18): Linear's GraphQL endpoint, the secret pointer to a read-only personal API key,
/// and the team keys that scope every read. Only <see cref="Create"/> makes one.
/// </summary>
public sealed record LinearSettings
{
    private LinearSettings(Uri url, string token, IReadOnlyList<string> teams)
    {
        Url = url;
        Token = token;
        Teams = teams;
    }

    /// <summary>Linear's GraphQL endpoint.</summary>
    public Uri Url { get; }

    /// <summary>The secret pointer to the API key, never the key itself.</summary>
    public string Token { get; }

    /// <summary>The team keys every read is limited to; empty reads every team the key can see.</summary>
    public IReadOnlyList<string> Teams { get; }

    /// <summary>Linear's host and port, as messages name it.</summary>
    public string Address => Url.Authority;

    /// <summary>Checks the profile's values.</summary>
    /// <param name="url">'context.sources.linear.url', or null for Linear's own API.</param>
    /// <param name="token">'context.sources.linear.token', a secret pointer.</param>
    /// <param name="teams">'context.sources.linear.teams' (already checked as team keys).</param>
    /// <returns>The settings, SND-CONTEXT-070 for a url that breaks the rules, or SND-CONTEXT-002 for no token or one that isn't a secret pointer (never repeated).</returns>
    public static Result<LinearSettings> Create(string? url, string? token, IReadOnlyList<string> teams) =>
        new LinearSettings(new Uri(url is { Length: > 0 } && Uri.IsWellFormedUriString(url, UriKind.Absolute) ? url : "https://stub.invalid/"), token ?? "", teams);
}
