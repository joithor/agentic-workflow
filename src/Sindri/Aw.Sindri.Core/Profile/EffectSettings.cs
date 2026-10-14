using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's effect keys (engine spec §5.7, §7.2 S1-D11; engine P6 unit (d)): 'workflow.effects', the allowlist
/// the dispatch recheck and RCP-EFFECT-001 read, and 'workflow.dispatchDelay', the undo window before each effect runs.
/// </summary>
/// <param name="Allowlist">The allowlisted effect ids; null when the profile has no 'workflow.effects' (RCP-EFFECT-001 skipped, every recipe effect refused with SND-WF-011).</param>
/// <param name="DispatchDelay">The delay before an effect's activity runs.</param>
public sealed record EffectSettings(IReadOnlySet<BlockId>? Allowlist, TimeSpan DispatchDelay)
{
    /// <summary>'workflow.dispatchDelay' when the profile leaves it out: 10 s (§5.7, the undo window).</summary>
    public static TimeSpan DefaultDispatchDelay { get; } = TimeSpan.FromSeconds(10);

    /// <summary>The effect keys <paramref name="profile"/> sets, the delay's default applied. Engine P6 unit (d).</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>The settings.</returns>
    public static EffectSettings Of(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        throw new NotSupportedException("engine P6 unit (d): EffectSettings.Of reads workflow.effects and workflow.dispatchDelay (S1-D11)");
    }
}
