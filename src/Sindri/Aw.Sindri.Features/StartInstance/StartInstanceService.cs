using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>'workflow start' (engine spec rev 8 §5.1, §6.1, §6.3). Engine P4 unit (b): red stub.</summary>
internal sealed class StartInstanceService(IItems items, IApprovedRecipes recipes, IInstanceDirectory directory, IFactStore facts, IInstanceRuntime runtime)
    : IService<StartInstanceRequest, StartInstanceResponse>
{
    public ValueTask<Result<StartInstanceResponse>> Handle(StartInstanceRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"engine P4 unit (b): StartInstance ({items}, {recipes}, {directory}, {facts}, {runtime})");
}
