using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListJobs;

/// <summary>The jobs list (API P6).</summary>
/// <param name="Jobs">The jobs the caller may see, newest first.</param>
/// <param name="Empty">The sentence for an empty list, or null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record JobList(IReadOnlyList<JobSummary> Jobs, string? Empty, AsOf AsOf);
