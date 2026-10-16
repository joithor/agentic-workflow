using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetJob;

/// <summary>A job as 'GET /sindri/jobs/{jobId}' serves it (API spec §3.1, API P6).</summary>
/// <param name="Id">The job.</param>
/// <param name="Kind">The catalog action that started it.</param>
/// <param name="Key">Its idempotency key within the kind.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Service">The service it runs, or null.</param>
/// <param name="State">Where it is.</param>
/// <param name="StartedBy">Who started it.</param>
/// <param name="StartedAt">When.</param>
/// <param name="EndedAt">When it ended, or null while running.</param>
/// <param name="Progress">The last 200 progress lines.</param>
/// <param name="Rounds">The highest progress round.</param>
/// <param name="Summary">How it ended in a line, or null while running.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
/// <param name="Result">The work's result (bounded, scrubbed), or null.</param>
/// <param name="CancelledBy">Who cancelled it, or null.</param>
/// <param name="Actions">'CancelJob' while running, none otherwise.</param>
/// <param name="Cli">'sindri jobs cancel JOB-…' while running (through CliForm.Fill), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record JobView(JobId Id, JobKind Kind, JobKey Key, ItemId? Item, RepoName? Repo, ServiceName? Service, JobState State, string StartedBy,
    DateTimeOffset StartedAt, DateTimeOffset? EndedAt, IReadOnlyList<JobLine> Progress, int Rounds, string? Summary, string? Code, JsonElement? Result,
    string? CancelledBy, IReadOnlyList<ActionId> Actions, string? Cli, AsOf AsOf);
