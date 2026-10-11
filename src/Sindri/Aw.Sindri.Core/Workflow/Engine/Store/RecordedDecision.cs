namespace Aw.Sindri.Core.Workflow.Engine.Store;

/// <summary>A decision as recorded under its key (spec W12, §5.3 step 2): what a replay of the same trigger returns.</summary>
/// <param name="Key">The trigger's key.</param>
/// <param name="Kind">Applied, refused or absorbed.</param>
/// <param name="Summary">What happened, as the CLI prints it ('-> checks', 'refused: re-running produce (round 2/3)', 'parked: rounds').</param>
/// <param name="Code">The error code a refused command got (SND-WF-004).</param>
public sealed record RecordedDecision(TriggerKey Key, RecordedKind Kind, string Summary, string? Code);
