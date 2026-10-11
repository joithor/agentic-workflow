namespace Aw.Judge.Core.Processes;

/// <summary>One program run: what to start, with what input, where, and for how long at most.</summary>
public sealed record ProcessRequest
{
    /// <summary>The program: a bare name is looked up on the search path, a path is used as is.</summary>
    public required string FileName { get; init; }

    /// <summary>The arguments, each passed as one argv entry (no shell quoting).</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Written to the program's standard input, which is then closed; null closes it at once.</summary>
    public string? StandardInput { get; init; }

    /// <summary>The working directory; null for the caller's.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Variables added to (or replacing in) the inherited environment.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>How long the program may run before it and every process it started are stopped.</summary>
    public required TimeSpan Timeout { get; init; }
}
