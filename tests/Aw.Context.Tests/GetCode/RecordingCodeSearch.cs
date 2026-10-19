using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>Passes every search on and keeps the queries, so a test can say what was (not) asked of git.</summary>
internal sealed class RecordingCodeSearch(ICodeSearch inner) : ICodeSearch
{
    internal List<CodeSearchQuery> Queries { get; } = [];

    public ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        Queries.Add(query);
        return inner.FilesNaming(query, cancellationToken);
    }

    public ValueTask<Result<string>> Resolve(ContextRepo repo, string commit, CancellationToken cancellationToken) => inner.Resolve(repo, commit, cancellationToken);
}
