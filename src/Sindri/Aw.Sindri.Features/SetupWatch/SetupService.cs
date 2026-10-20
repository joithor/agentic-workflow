using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>The 'setup' built-in service (setup-once spec §5): the shell.</summary>
#pragma warning disable MA0182 // red stub: registered with the implementation
internal sealed class SetupService : IScheduledService
{
    internal static ServiceName ServiceId => throw new NotSupportedException();

    internal static BuiltInService BuiltIn => throw new NotSupportedException();

    public ServiceName Identity() => throw new NotSupportedException();

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) => throw new NotSupportedException();
}
