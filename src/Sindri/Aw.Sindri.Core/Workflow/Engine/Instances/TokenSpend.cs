namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// An instance's token spend (spec §3.7, W7): against the recipe budget and each state's budget, plus what grants added.
/// An instance that began under the per-round rule has <see cref="Rounds"/> set (the marker); one that began before it
/// has none and keeps the pooled rule, where a state's budget is its lifetime total (<see cref="ByState"/>).
/// </summary>
/// <param name="Total">Tokens spent by every run.</param>
/// <param name="ByState">Tokens spent per state, over every round (what the pooled rule counts against the state's budget).</param>
/// <param name="Granted">Tokens added by '--grant-tokens' answers (they raise the recipe's allowance, and the round's they answered).</param>
public sealed record TokenSpend(int Total, IReadOnlyDictionary<string, int> ByState, int Granted)
{
    /// <summary>Nothing spent, under the pooled rule (an instance from before per-round budgets).</summary>
    public static TokenSpend None { get; } = new(0, new Dictionary<string, int>(StringComparer.Ordinal), 0);

    /// <summary>Nothing spent, under the per-round rule (every instance that starts now).</summary>
    public static TokenSpend PerRound { get; } = None with { Rounds = new Dictionary<string, RoundSpend>(StringComparer.Ordinal), Overruns = [] };

    /// <summary>The latest round of each state and what it has spent and been granted; null under the pooled rule (the version marker).</summary>
    public IReadOnlyDictionary<string, RoundSpend>? Rounds { get; init; }

    /// <summary>The rounds that spent more than their allowance (a run is checked only after it reports), latest per state and round; null under the pooled rule.</summary>
    public IReadOnlyList<RoundOverrun>? Overruns { get; init; }

    /// <summary>Whether a state's budget applies to each round of it, not to the state's lifetime.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPerRound => Rounds is not null;
}
