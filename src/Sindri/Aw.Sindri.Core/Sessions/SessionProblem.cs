namespace Aw.Sindri.Core.Sessions;

/// <summary>A source that failed in a pass (collector spec §4.1): a fixed sentence per code, never an exception message.</summary>
/// <param name="Source">The source: 'processes', 'claude-records', 'fleet-states' or 'collector'.</param>
/// <param name="Code">The stable code, for example 'SND-SESSION-003'.</param>
/// <param name="Text">The sentence a client shows.</param>
public sealed record SessionProblem(string Source, string Code, string Text);
