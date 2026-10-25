using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What the concept-scan jobs already did, read from the jobs view.</summary>
internal static class ConceptScanLog
{
    internal const string OverBudget = "over-budget";

    internal static int Started(IEnumerable<JobRow> jobs, DateTimeOffset since) => throw new NotSupportedException();

    internal static bool Handled(IEnumerable<JobRow> jobs, int version) => throw new NotSupportedException();

    internal static bool FailedSince(IEnumerable<JobRow> jobs, DateTimeOffset since) => throw new NotSupportedException();
}
