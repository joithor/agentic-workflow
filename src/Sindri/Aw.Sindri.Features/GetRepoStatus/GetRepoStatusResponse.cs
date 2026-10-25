using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetRepoStatus;

/// <summary>The repo's state.</summary>
/// <param name="Status">The status.</param>
public sealed record GetRepoStatusResponse(RepoStatus Status);
