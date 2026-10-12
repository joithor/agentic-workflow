using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>'workflow answer', 'command' and 'cancel' (engine spec rev 8 §5.2, §5.8, §6.3). Engine P4 unit (b): red stub.</summary>
internal sealed class ApplyCommandService(IInstanceDirectory directory, IInstanceRuntime runtime, IApprovedRecipes recipes, ICommandNonces nonces, IChallenges challenges)
    : IService<ApplyCommandRequest, ApplyCommandResponse>
{
    public ValueTask<Result<ApplyCommandResponse>> Handle(ApplyCommandRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"engine P4 unit (b): ApplyCommand ({directory}, {runtime}, {recipes}, {nonces}, {challenges})");
}
