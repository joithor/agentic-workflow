using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The work of a backtest job: runs the engine's backtest under the starting caller and answers the small result.</summary>
internal sealed class RunContextBacktestService : IService<RunContextBacktestRequest, RunContextBacktestResult>
{
    public ValueTask<Result<RunContextBacktestResult>> Handle(RunContextBacktestRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
