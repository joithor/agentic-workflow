using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IOnyxWorkspace"/> under test with the paths it uses and a way to put raw text where its state lives.</summary>
public abstract class OnyxWorkspaceRig : IDisposable
{
    /// <summary>Gets the port under test.</summary>
    public abstract IOnyxWorkspace Workspace { get; }

    /// <summary>Gets the paths its files live at.</summary>
    public abstract OnyxPaths Paths { get; }

    /// <summary>Replaces the state file's content with raw text (to corrupt it).</summary>
    /// <param name="text">The text.</param>
    public abstract void WriteRawState(string text);

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
