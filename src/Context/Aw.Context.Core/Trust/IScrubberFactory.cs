using Aw.Kernel;

namespace Aw.Context.Core.Trust;

/// <summary>Builds a call's <see cref="Scrubbers"/> from the profile's extra patterns (spec §7, D14: one scrubber for the whole build).</summary>
public interface IScrubberFactory
{
    /// <summary>The scrubbers for <paramref name="extraPatterns"/>: the built-in rules, then the extra patterns in order.</summary>
    /// <param name="extraPatterns">The profile's extra patterns (empty for none).</param>
    /// <returns>The scrubbers, or SND-CONTEXT-001 naming the first refused entry's key path ('context.scrub.extraPatterns[0].pattern: …').</returns>
    public Result<Scrubbers> For(IReadOnlyList<ScrubPattern> extraPatterns);
}
