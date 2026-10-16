namespace Aw.Context.Tests.BuildMap;

/// <summary>An <see cref="IProgress{T}"/> that records on the calling thread, so a test reads the lines in the order they were told.</summary>
internal sealed class SyncProgress : IProgress<string>
{
    public List<string> Lines { get; } = [];

    public void Report(string value) => Lines.Add(value);
}
