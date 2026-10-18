namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The environment allowlist (sandbox spec §3.4): one list in code with a reason per name, and the secret-pattern check.</summary>
public static class EnvironmentAllowlist
{
    /// <summary>Every name the plan may set, with the reason it is on the list.</summary>
    public static IReadOnlyDictionary<string, string> Names => throw new NotSupportedException();

    /// <summary>Whether a name looks like a secret: *_TOKEN, *_KEY, *_SECRET, *PASSWORD*, AWS_*, GH_*, GITHUB_*, AGENT_APP_*.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True when it does.</returns>
    public static bool IsSecretShaped(string name) => throw new NotSupportedException();

    /// <summary>The names that are secret-shaped and not on the list with a reason.</summary>
    /// <param name="names">Variable names.</param>
    /// <returns>The offenders, in order.</returns>
    public static IReadOnlyList<string> Violations(IEnumerable<string> names) => throw new NotSupportedException();
}
