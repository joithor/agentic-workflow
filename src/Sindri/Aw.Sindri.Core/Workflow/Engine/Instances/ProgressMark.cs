namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>What a loop round produced, for the no-progress signals (spec §3.7, A §17.23.13): digests, never content.</summary>
/// <param name="Findings">The SHA-256 of the round's findings (the 'fact:findings' it produced), if any.</param>
/// <param name="Failure">The SHA-256 of the round's failure (a step.failed reason or the refusing gate's reasons), if any.</param>
/// <param name="EmptyDiff">Whether the round changed nothing.</param>
/// <param name="FindingKeys">The sorted keys of the round's findings when they came in the keyed shape; null otherwise (the digest compares then).</param>
public sealed record ProgressMark(string? Findings, string? Failure, bool EmptyDiff, IReadOnlyList<string>? FindingKeys = null);
