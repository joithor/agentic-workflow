using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The refusal of a start whose (kind, key) is already running (SND-JOB-409): the start service and an admission that checks first share it.</summary>
public static class JobConflicts
{
    /// <summary>The refusal.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="key">The key.</param>
    /// <param name="running">The job that holds the claim.</param>
    /// <returns>SND-JOB-409 with the 'runningJob' extension.</returns>
    public static Error Running(JobKind kind, JobKey key, JobId running)
    {
        using var id = JsonDocument.Parse($"\"{running.Value}\"");   // a job id is 'JOB-' and hex digits: nothing to escape
        return SindriErrors.JobConflict.With($"A {kind.Value} job is already running for {key.Value} (fix: run `sindri jobs show {running.Value}`, or wait for it to end)").WithExtension("runningJob", id.RootElement.Clone());
    }
}
