using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IProcessLocations on macOS through lsof (one call, run by <see cref="MacHostReader"/>).</summary>
/// <param name="reader">Runs lsof.</param>
/// <param name="selfPid">The scanning process's id.</param>
internal sealed class MacProcessLocations(MacHostReader reader, int selfPid) : IProcessLocations
{
    public async ValueTask<Result<IReadOnlyList<ProcessLocation>>> Locations(CancellationToken cancellationToken)
    {
        var output = await reader.Lsof(cancellationToken).ConfigureAwait(false);
        if (output.IsError)
        {
            return output.Error;
        }

        var found = LsofOutput.Parse(output.Value);
        return found.Count == 0
            ? MetaErrors.HostUnreadable.With("unavailable on macOS: lsof listed no process")
            : Results.Success<IReadOnlyList<ProcessLocation>>([.. found.Select(e => e.Location.Pid == selfPid ? e.Location with { IsInvoker = true } : e.Location)]);
    }
}
