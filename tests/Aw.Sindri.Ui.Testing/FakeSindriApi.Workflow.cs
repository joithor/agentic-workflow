using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The workflow reads of the fake: served from the goldens in the contract slice that follows the client.</summary>
internal sealed partial class FakeSindriApi
{
    public Task<Result<InstanceList>> Instances(Uri api, InstanceQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<InstanceDetail>> Instance(Uri api, string id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<FactView>> InstanceFact(Uri api, string id, string key, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<RecipeView>> Recipe(Uri api, string recipe, string? hash, CancellationToken cancellationToken) => throw new NotSupportedException();
}
