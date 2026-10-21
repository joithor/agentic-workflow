using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>Each repo's index state, for the caller's repos only; a named repo outside them is the uniform not-found (SND-CODEIDX-013).</summary>
internal sealed class GetCodeIndexStatusService(CodeIndexScopes scopes, ICodeIndex index) : IService<GetCodeIndexStatusRequest, CodeIndexStatusView>
{
    public async ValueTask<Result<CodeIndexStatusView>> Handle(GetCodeIndexStatusRequest request, CancellationToken cancellationToken)
    {
        var repo = CodeIndexInputs.Repo(request.Repo);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (repo.IsError || scope.IsError)
        {
            return repo.IsError ? repo.Error : scope.Error;
        }

        var answer = await index.Status(scope.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        if (repo.Value is not { } wanted)
        {
            return new CodeIndexStatusView(answer.Value.Repos);
        }

        // The facade's own words, so a repo outside the scope reads like one never built.
        var only = answer.Value.Repos.Where(r => r.Repo == wanted).ToList();
        return only.Count == 0 ? CodeIndexErrors.IndexNotBuilt.With(CodeIndexErrors.IndexNotBuilt.Title) : new CodeIndexStatusView(only);
    }
}
