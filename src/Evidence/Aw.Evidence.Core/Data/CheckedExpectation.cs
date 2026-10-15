namespace Aw.Evidence.Core.Data;

/// <summary>The outcome of checking one expectation.</summary>
/// <param name="Count">How many assertions it holds (the status or exit code, each pointer, the golden).</param>
/// <param name="Failures">The ones that did not hold.</param>
/// <param name="FirstFailedOrdinal">The 1-based position, among its assertions, of the first that failed; null when all held.</param>
public sealed record CheckedExpectation(int Count, IReadOnlyList<ApiFailure> Failures, int? FirstFailedOrdinal);
