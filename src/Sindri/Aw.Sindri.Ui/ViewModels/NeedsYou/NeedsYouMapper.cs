using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>Maps the API's needs-you list, with the command catalog and the pending decisions, onto the rows and feed the view models take.</summary>
#pragma warning disable MA0182 // why: the shell reads it in the next commit (T5 re-reads)
internal static class NeedsYouMapper
{
    /// <summary>The feed for a served list.</summary>
    /// <param name="list">GET /needs-you.</param>
    /// <param name="catalog">GET /commands: where each action's readiness comes from.</param>
    /// <param name="decisions">The pending decisions, whose options become a decision row's Decide buttons; null keeps the one served action.</param>
    /// <param name="counts">Home's other totals.</param>
    /// <returns>The feed, rows in the API's order.</returns>
    internal static NeedsYouFeed Map(NeedsYouList list, CommandList catalog, DecisionList? decisions, HomeCounts counts) => throw new NotSupportedException();
}
