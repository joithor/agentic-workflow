using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IProcessLocations: the one fake, kept honest by ProcessLocationsContract.</summary>
public sealed class InMemoryProcessLocations : IProcessLocations
{
    private readonly List<ProcessLocation> _processes = [];
    private int _next = 9000;

    /// <summary>When set, every scan fails with this error (a scan that can't run).</summary>
    public Error? Failure { get; set; }

    /// <summary>Starts a process whose working directory is <paramref name="directory"/>.</summary>
    /// <param name="directory">The working directory.</param>
    /// <param name="command">The command line.</param>
    /// <returns>Its pid.</returns>
    public int StartIn(string directory, string command = "sleep 120")
    {
        ArgumentNullException.ThrowIfNull(directory);
        var pid = ++_next;
        _processes.Add(new ProcessLocation(pid, command, [directory]));
        return pid;
    }

    /// <summary>Adds a process as given.</summary>
    /// <param name="process">The process.</param>
    public void Add(ProcessLocation process) => _processes.Add(process);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<ProcessLocation>>> Locations(CancellationToken cancellationToken) =>
        Failure is { } error ? Done.Failed<IReadOnlyList<ProcessLocation>>(error) : Done.Of<IReadOnlyList<ProcessLocation>>([.. _processes]);
}
