namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>An instance's token spend (spec §3.7, W7): against the recipe budget and each state's budget, plus what grants added.</summary>
/// <param name="Total">Tokens spent by every run.</param>
/// <param name="ByState">Tokens spent per state.</param>
/// <param name="Granted">Tokens added by '--grant-tokens' answers.</param>
public sealed record TokenSpend(int Total, IReadOnlyDictionary<string, int> ByState, int Granted)
{
    /// <summary>Nothing spent.</summary>
    public static TokenSpend None { get; } = new(0, new Dictionary<string, int>(StringComparer.Ordinal), 0);
}
