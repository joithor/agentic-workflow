using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// Embeds one repo's pending symbols (code index spec §6.3, §8): a job-class command the 'code-index-embed' service starts as itself while the repo's embeddings layer is pending. It takes no heavy-job slot.
/// Keyed 'embed:&lt;repo&gt;', so a second start for the same repo is SND-JOB-409. It has no route or verb of its own.
/// </summary>
/// <param name="Repo">The registered repo.</param>
public sealed record EmbedCodeIndexRequest(string Repo) : IJob<EmbedCodeIndexResult>;
