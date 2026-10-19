using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Onyx;

internal sealed class DockerExecOnyxDatabase : IOnyxDatabase
{
    public DockerExecOnyxDatabase(IToolRunner runner)
    {
        _ = runner;
    }

    public Result<int?> DefaultAdminGroup(ComposeProject project, CancellationToken cancellationToken)
        => throw new NotSupportedException("red");
}
