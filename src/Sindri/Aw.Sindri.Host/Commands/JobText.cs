using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.ListJobs;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri jobs' and 'jobs show' as text.</summary>
internal static class JobText
{
    /// <summary>One line per job: 'id  state  kind  service-or-key  started  N lines  [summary] [code]'.</summary>
    /// <param name="list">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(JobList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }

        foreach (var j in list.Jobs)
        {
            var end = string.Join(' ', new[] { j.Summary, j.Code }.Where(static p => p is not null));
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{j.Id.Value}  {State(j.State)}  {j.Kind.Value}  {j.Service?.Value ?? j.Key.Value}  {Time(j.StartedAt)}  {j.Rounds} lines{(end.Length == 0 ? string.Empty : "  " + end)}"));
        }

        return text.ToString();
    }

    /// <summary>A job: its header, who started it and when, its lines, how it ended and the command that cancels it.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The text.</returns>
    internal static string Render(JobView job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var text = new StringBuilder();
        _ = text.AppendLine($"{job.Id.Value}  {State(job.State)}  {job.Kind.Value}  {job.Service?.Value ?? job.Key.Value}");
        _ = text.AppendLine($"started by {job.StartedBy} at {Time(job.StartedAt)}");
        foreach (var line in job.Progress)
        {
            _ = text.AppendLine(line.Line);
        }

        if (JobFrames.Ended(job) is { } ended)
        {
            _ = text.AppendLine(ended.Text);
        }

        if (job.Result is { } result)
        {
            _ = text.Append("result: ").AppendLine(result.GetRawText());
        }

        return job.Cli is { } cli ? text.Append("cancel: ").AppendLine(cli).ToString() : text.ToString();
    }

    private static string Time(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string State(JobState state) => state switch
    {
        JobState.Running => "running",
        JobState.Succeeded => "succeeded",
        JobState.Failed => "failed",
        JobState.Cancelled => "cancelled",
        _ => "unknown",
    };
}
