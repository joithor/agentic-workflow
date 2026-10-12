using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>
/// <see cref="ICommandOrigins"/> over the ledger (engine spec rev 8 §5.4; engine P4 unit (d)): the causing event, found by
/// its envelope id, names its instance in its payload's 'instance' field, as every 'workflow.*' event the interpreter
/// mirrors does. P6's 'workflow_commands' map replaces the payload read.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class LedgerCommandOrigins(LedgerDatabase database) : ICommandOrigins
{
    public ValueTask<Result<InstanceAddress?>> CauserOf(LedgerEvent reply, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"engine P4 unit (d): the causing command's instance ({database.GetType().Name})");
}
