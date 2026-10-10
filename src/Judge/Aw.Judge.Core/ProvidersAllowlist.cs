using Aw.Judge.Core.Providers;
using Aw.Kernel;

namespace Aw.Judge.Core;

/// <summary>
/// The providers allowlist: the global flag '--providers a,b', accepted anywhere in argv so it composes with every
/// command, else AW_JUDGE_PROVIDERS (hooks can't always add flags). The Host prints an error as 'judge: &lt;message&gt;'
/// and exits 64. It sits beside JudgeErrors so the Providers namespace stays below the root (no namespace cycle).
/// </summary>
/// <param name="Allowed">The allowed providers, deduplicated in first-seen order; null means every provider is allowed.</param>
/// <param name="Arguments">argv without the flag and its value.</param>
public sealed record ProvidersAllowlist(IReadOnlyList<ProviderName>? Allowed, IReadOnlyList<string> Arguments)
{
    private const string Flag = "--providers";

    /// <summary>Reads the allowlist from argv, else from the AW_JUDGE_PROVIDERS value.</summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="environmentValue">AW_JUDGE_PROVIDERS, or null when unset.</param>
    /// <returns>The allowlist, or AW-JUDGE-010 whose message is the TS error text.</returns>
    public static Result<ProvidersAllowlist> Parse(IReadOnlyList<string> arguments, string? environmentValue)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var at = IndexOf(arguments);
        string raw;
        var rest = arguments.ToList();
        if (at >= 0)
        {
            if (at + 1 >= arguments.Count)
            {
                return JudgeErrors.ProvidersFlag.With("--providers needs a comma-separated list");
            }

            raw = arguments[at + 1];
            rest.RemoveRange(at, 2);
        }
        else if (environmentValue is not null)
        {
            raw = environmentValue;
        }
        else
        {
            return new ProvidersAllowlist(Allowed: null, rest);
        }

        return Names(raw).Map(allowed => new ProvidersAllowlist(allowed, rest));
    }

    /// <summary>Whether a provider may be called.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>True when there is no allowlist or it lists the provider.</returns>
    public bool IsAllowed(ProviderName provider) => Allowed is null || Allowed.Contains(provider);

    private static int IndexOf(IReadOnlyList<string> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (string.Equals(arguments[i], Flag, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static Result<IReadOnlyList<ProviderName>> Names(string raw)
    {
        var names = raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (names.Count == 0)
        {
            return Results.Failure<IReadOnlyList<ProviderName>>(JudgeErrors.ProvidersFlag.With("--providers list is empty"));
        }

        var bad = names.Where(n => !ProviderNames.TryParse(n, out _)).ToList();
        if (bad.Count > 0)
        {
            return Results.Failure<IReadOnlyList<ProviderName>>(JudgeErrors.ProvidersFlag.With($"unknown provider(s): {string.Join(", ", bad)}"));
        }

        IReadOnlyList<ProviderName> allowed = [.. names.Select(n => ProviderNames.TryParse(n, out var p) ? p : default).Distinct()];
        return Results.Success(allowed);
    }
}
