namespace Aw.Sindri.Tests.Locks;

/// <summary>An <see cref="IProgress{T}"/> that records on the calling thread, so a test reads the lines as soon as the call returns.</summary>
internal sealed class SyncProgress : IProgress<string>
{
    public List<string> Lines { get; } = [];

    public void Report(string value) => Lines.Add(value);
}
