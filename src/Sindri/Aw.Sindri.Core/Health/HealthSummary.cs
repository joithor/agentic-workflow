using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Health;

/// <summary>Folds the status inputs into one <see cref="HealthReport"/> (UI spec E-14; P2 plan G10): stalled &gt; rebuilding &gt; degraded &gt; healthy.</summary>
public static class HealthSummary
{
    /// <summary>The health for these inputs.</summary>
    /// <param name="running">Whether this process holds the lease.</param>
    /// <param name="views">The projector's status.</param>
    /// <param name="services">The services view's rows.</param>
    /// <param name="profileNeedsApproval">True when no profile is approved.</param>
    /// <param name="profileChanged">True when the live profile changed since approval.</param>
    /// <returns>The health.</returns>
    public static HealthReport For(bool running, ViewsStatus views, IReadOnlyList<ServiceRow> services, bool profileNeedsApproval, bool profileChanged)
        => throw new NotSupportedException("HealthSummary.For: P2 unit E makes Health_word_follows_the_precedence pass");
}
