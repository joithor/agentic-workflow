using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Checks;

/// <summary><see cref="ICheckCommands"/> over <see cref="IToolRunner"/>: the command runs in the worktree, and what it printed is scrubbed and cut to its tail before anyone sees it.</summary>
internal sealed class ToolCheckCommands : ICheckCommands
{
    public ToolCheckCommands(IToolRunner runner, ITextScrubber scrubber, TimeProvider clock)
    {
        _ = (runner, scrubber, clock);
    }

    public ValueTask<Result<CheckRun>> Run(CheckInvocation command, string workingDirectory, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red");
}
