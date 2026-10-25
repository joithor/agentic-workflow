using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.GhPublish;

/// <summary>Pushes a run's images to the PR's evidence branch and writes the PR comment that embeds them.</summary>
/// <param name="RunDirectory">The run directory holding parity.json (a parity run) or summary.json (absolute).</param>
/// <param name="Repo">The repository, owner/name; always passed to gh as a path, never resolved from the working directory or GH_REPO.</param>
/// <param name="Pr">The pull request number, 1 or more.</param>
/// <param name="Provenance">The database provenance the caller determined; a parity run carries its own, so null is fine there.</param>
/// <param name="Reviewed">The image files already opened and reviewed (absolute, as the run lists them).</param>
public sealed record GhPublishRequest(string RunDirectory, string Repo, int Pr, DbProvenance? Provenance, IReadOnlyList<string> Reviewed);
