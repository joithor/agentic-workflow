using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing.Forge;

/// <summary>A pull-request port ready for a contract run.</summary>
/// <param name="Pulls">The port.</param>
/// <param name="Repo">A repository on an allowed host that the run may use.</param>
/// <param name="ForeignHost">A host the port's host list does not allow.</param>
public sealed record PullRequestsUnderTest(IPullRequests Pulls, PullRepo Repo, string ForeignHost);
