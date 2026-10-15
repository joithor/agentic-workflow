namespace Aw.Sindri.Core.Health;

/// <summary>The control plane's health on GET /status (UI spec E-14): one word and the problems behind it, in a fixed order.</summary>
/// <param name="Word">The word.</param>
/// <param name="Problems">The problems, empty when healthy.</param>
/// <param name="Sentence">"Sindri is degraded: A, B, and C." built from the problems' clauses; null when healthy.</param>
public sealed record HealthReport(HealthWord Word, IReadOnlyList<HealthProblem> Problems, string? Sentence);
