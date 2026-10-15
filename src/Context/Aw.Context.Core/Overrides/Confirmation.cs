using Aw.Context.Core.Results;

namespace Aw.Context.Core.Overrides;

/// <summary>The one rule for 'confirmed' (S-4): a person or the extractor stands behind it. Lenses coverage, file-map status and the review queue all use it.</summary>
public static class Confirmation
{
    /// <summary>Whether something is confirmed: human or deterministic provenance, or an accept override over a model one.</summary>
    /// <param name="provenance">Who produced the node or link.</param>
    /// <param name="accepted">Whether an active accept override covers it (the applied graph already shows that as human provenance).</param>
    /// <returns>True when confirmed.</returns>
    public static bool Of(Provenance provenance, bool accepted = false) => accepted || provenance != Provenance.Model;
}
