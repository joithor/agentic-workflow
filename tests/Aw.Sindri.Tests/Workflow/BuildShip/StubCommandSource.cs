using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>The repo's commands, as the test lists them, whatever stage each names.</summary>
/// <param name="commands">The commands.</param>
internal sealed class StubCommandSource(IReadOnlyList<BuildCommand> commands) : IBuildCommandSource
{
    public ValueTask<Result<IReadOnlyList<BuildCommand>>> For(string stage, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<BuildCommand>>([.. commands.Where(c => string.Equals(c.Where, stage, StringComparison.Ordinal))]));
}
