using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.Resume;

internal sealed class ResumeService(IStateStore store) : IService<ResumeRequest, ResumeResponse>
{
    public ValueTask<Result<ResumeResponse>> Handle(ResumeRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Load(request.StateDirectory)
            .Map(state => new ResumeResponse(state.Phase, state.Status, state.Attempt, NextStep.For(state))));
}
