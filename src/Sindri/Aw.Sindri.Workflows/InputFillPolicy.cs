using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How long a Step input's fill may run and how a failed one is reported (SND-WF-020).</summary>
internal static class InputFillPolicy
{
    /// <summary>How often a running fill reports that it is alive.</summary>
    internal static readonly TimeSpan BeatEvery = TimeSpan.FromSeconds(15);

    internal static ActivityOptions Options(string activityId, string taskQueue) => throw new NotSupportedException();

    internal static string Reason(string input, Exception? inner) => throw new NotSupportedException();
}
