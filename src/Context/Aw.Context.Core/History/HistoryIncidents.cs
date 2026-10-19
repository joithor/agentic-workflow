using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.History;

/// <summary>The incident source's answer: none exists yet, so it reads not-configured (additive: a 'reason' beside the spec's state and items).</summary>
/// <param name="State">The state word.</param>
/// <param name="Reason">The recorded-limit word.</param>
/// <param name="Items">The incidents found.</param>
public sealed record HistoryIncidents(SearchedState State, string Reason, IReadOnlyList<string> Items)
{
    /// <summary>No incident source is configured.</summary>
    public static HistoryIncidents NotConfigured { get; } = new(SearchedState.NotConfigured, "not-configured", []);
}
