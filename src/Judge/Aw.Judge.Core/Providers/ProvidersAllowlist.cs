using Aw.Kernel;

namespace Aw.Judge.Core.Providers;

/// <summary>
/// The providers allowlist: the global flag '--providers a,b', accepted anywhere in argv so it composes with every
/// command, else AW_JUDGE_PROVIDERS (hooks can't always add flags). The Host prints an error as 'judge: &lt;message&gt;'
/// and exits 64.
/// </summary>
/// <param name="Allowed">The allowed providers, deduplicated in first-seen order; null means every provider is allowed.</param>
/// <param name="Arguments">argv without the flag and its value.</param>
public sealed record ProvidersAllowlist(IReadOnlyList<ProviderName>? Allowed, IReadOnlyList<string> Arguments)
{
    /// <summary>Reads the allowlist from argv, else from the AW_JUDGE_PROVIDERS value.</summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="environmentValue">AW_JUDGE_PROVIDERS, or null when unset.</param>
    /// <returns>The allowlist, or AW-JUDGE-010 whose message is the TS error text.</returns>
    public static Result<ProvidersAllowlist> Parse(IReadOnlyList<string> arguments, string? environmentValue) =>
        throw new NotSupportedException("red: parse the allowlist");

    /// <summary>Whether a provider may be called.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>True when there is no allowlist or it lists the provider.</returns>
    public bool IsAllowed(ProviderName provider) => Allowed is null || Allowed.Contains(provider);
}
