using System.Diagnostics.CodeAnalysis;

namespace Aw.Judge.Core.Providers;

/// <summary>The providers' wire names, as the CLI flag, the config file and the decision rows spell them.</summary>
public static class ProviderNames
{
    /// <summary>The wire name, e.g. 'claude-cli'.</summary>
    /// <param name="value">The provider.</param>
    /// <returns>The name.</returns>
    public static string Name(ProviderName value) => value switch
    {
        ProviderName.Rules => "rules",
        ProviderName.ClaudeCli => "claude-cli",
        ProviderName.CodexCli => "codex-cli",
        ProviderName.CursorCli => "cursor-cli",
        ProviderName.Jev => "jev",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "unknown provider"),
    };

    /// <summary>Reads a wire name, exactly (no trimming, case-sensitive), as the TS isProviderName does.</summary>
    /// <param name="text">The name.</param>
    /// <param name="value">The provider, when the name is one.</param>
    /// <returns>Whether it is a provider name.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out ProviderName value)
    {
        (var ok, value) = text switch
        {
            "rules" => (true, ProviderName.Rules),
            "claude-cli" => (true, ProviderName.ClaudeCli),
            "codex-cli" => (true, ProviderName.CodexCli),
            "cursor-cli" => (true, ProviderName.CursorCli),
            "jev" => (true, ProviderName.Jev),
            _ => (false, default),
        };
        return ok;
    }
}
