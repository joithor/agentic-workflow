namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What the 'GetInstance' Query returns (engine spec rev 8 §5.1 "Queries"). Engine P0 spike: the state, the open run
/// token and the outcome; P4 adds the rendered 'next', allowedCommands, the open task and the children.
/// </summary>
/// <param name="State">The state status shows ('starting' before the recipe loaded).</param>
/// <param name="Run">The open run's token, in an active state.</param>
/// <param name="Outcome">The terminal outcome, once the instance ended.</param>
internal sealed record InstanceSnapshot(string State, string? Run, string? Outcome);
