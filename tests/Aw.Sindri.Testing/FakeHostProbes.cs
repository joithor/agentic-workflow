using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Host probes over a scripted file system and port table. Held to <see cref="HostProbesContract"/>.</summary>
public sealed class FakeHostProbes : IHostProbes
{
    /// <summary>Gets the directories that exist; a path walks up to the nearest of these (the root always exists).</summary>
    public ISet<string> Directories { get; } = new HashSet<string>(StringComparer.Ordinal) { "/" };

    /// <summary>Gets or sets the free bytes every directory reports.</summary>
    public long FreeBytes { get; set; } = 100L << 30;

    /// <summary>Gets the ports that can't be bound.</summary>
    public ISet<int> TakenPorts { get; } = new HashSet<int>();

    /// <inheritdoc />
    public DiskFacts FreeDisk(string path)
    {
        var measured = path;
        while (!Directories.Contains(measured) && Path.GetDirectoryName(measured) is { Length: > 0 } parent)
        {
            measured = parent;
        }

        return new DiskFacts(Directories.Contains(measured) ? measured : "/", FreeBytes);
    }

    /// <inheritdoc />
    public PortFacts Ports(int preferred, int fallbackFirst, int fallbackLast)
    {
        if (!TakenPorts.Contains(preferred))
        {
            return new PortFacts(preferred, PreferredFree: true, FirstFreeFallback: null);
        }

        var fallback = Enumerable.Range(fallbackFirst, Math.Max(0, fallbackLast - fallbackFirst + 1)).Where(p => !TakenPorts.Contains(p)).Cast<int?>().FirstOrDefault();
        return new PortFacts(preferred, PreferredFree: false, fallback);
    }
}
