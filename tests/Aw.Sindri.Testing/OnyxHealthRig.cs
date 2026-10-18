using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IOnyxHealth"/> under test and the port to probe.</summary>
public abstract class OnyxHealthRig : IDisposable
{
    /// <summary>Gets the port under test.</summary>
    public abstract IOnyxHealth Health { get; }

    /// <summary>Gets the port to probe.</summary>
    public abstract int Port { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases what a concrete rig holds.</summary>
    /// <param name="disposing">True when called from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
