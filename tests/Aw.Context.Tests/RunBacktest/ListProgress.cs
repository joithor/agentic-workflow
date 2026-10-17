namespace Aw.Context.Tests.RunBacktest;

/// <summary>Progress lines kept in order, synchronously (Progress of T posts to the thread pool).</summary>
internal sealed class ListProgress : IProgress<string>
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return [.. _lines];
            }
        }
    }

    public void Report(string value)
    {
        lock (_lines)
        {
            _lines.Add(value);
        }
    }
}
