using Aw.Context.Core.Graph;
using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>An <see cref="IScopeRunProposals"/> that records each call and answers every surface as new (with a node), or answers the error it was given.</summary>
internal sealed class FakeRunProposals : IScopeRunProposals
{
    private readonly Error? _error;

    /// <summary>Answers every call.</summary>
    public FakeRunProposals()
    {
        _error = null;
    }

    /// <summary>Fails every call with the error.</summary>
    /// <param name="error">The error.</param>
    public FakeRunProposals(Error error)
    {
        _error = error;
    }

    /// <summary>The run, repo and surfaces of each call, in order.</summary>
    public List<(string Run, RepoName Repo, IReadOnlyList<ProposedSurface> Surfaces)> Calls { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<ProposeContextMapResponse>> Replace(string run, RepoName repo, IReadOnlyList<ProposedSurface> surfaces, CancellationToken cancellationToken)
    {
        Calls.Add((run, repo, surfaces));
        if (_error is { } error)
        {
            return ValueTask.FromResult<Result<ProposeContextMapResponse>>(error);
        }

        var statuses = surfaces.Select(static s => new GraphStatus(s.Name, NodeKey.From("surface:dialog/" + (s.Name.Contains("Expiry", StringComparison.Ordinal) ? "share-expiry" : "link-expired")), GraphStatusKind.New)).ToList();
        return ValueTask.FromResult<Result<ProposeContextMapResponse>>(new ProposeContextMapResponse(new ProposeResult(Draft: null, surfaces.Count, Existing: 0, Rejected: 0), Sequence: null, statuses));
    }
}
