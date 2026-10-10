namespace Aw.Readiness.Core;

/// <summary>One readiness criterion: what it checks, where it sits and how to fix a failure.</summary>
/// <param name="Id">The stable id, such as SV-1.</param>
/// <param name="Pillar">The pillar it belongs to.</param>
/// <param name="Level">The level (1-5) it counts toward.</param>
/// <param name="Title">What it checks, in a few words.</param>
/// <param name="Fix">What to do when it fails.</param>
public sealed record Criterion(CriterionId Id, Pillar Pillar, int Level, string Title, string Fix);
