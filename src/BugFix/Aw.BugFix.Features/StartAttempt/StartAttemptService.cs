using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.StartAttempt;

internal sealed class StartAttemptService : IService<StartAttemptRequest, StartAttemptResponse>
{
    public ValueTask<Result<StartAttemptResponse>> Handle(StartAttemptRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("StartAttempt: make StartAttemptTests pass");
}
