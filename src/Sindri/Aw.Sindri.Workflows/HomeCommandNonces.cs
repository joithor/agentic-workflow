using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary><see cref="ICommandNonces"/> as owner-only files under the home (red: engine P4 unit (c)).</summary>
internal sealed class HomeCommandNonces : ICommandNonces
{
    public HomeCommandNonces(WorkflowsHome home, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public CommandNonce Issue() => throw new NotSupportedException("red: engine P4 unit (c)");

    public ValueTask<Result<bool>> Claim(CommandNonce nonce, string subject, CancellationToken cancellationToken) => throw new NotSupportedException("red: engine P4 unit (c)");

    public ValueTask<Result<Unit>> Applied(CommandNonce nonce, CancellationToken cancellationToken) => throw new NotSupportedException("red: engine P4 unit (c)");

    public ValueTask<Result<Unit>> Release(CommandNonce nonce, CancellationToken cancellationToken) => throw new NotSupportedException("red: engine P4 unit (c)");
}
