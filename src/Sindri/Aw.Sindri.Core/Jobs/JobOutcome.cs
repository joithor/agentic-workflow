using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Jobs;

/// <summary>How a job's work ended, as 'job.finished' records it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobOutcome>))]
public enum JobOutcome
{
    /// <summary>The work returned a result.</summary>
    [JsonStringEnumMemberName("succeeded")]
    Succeeded,

    /// <summary>The work returned an error or threw.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
