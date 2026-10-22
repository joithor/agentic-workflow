using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>A parser that answers what the test registered per path, so the build can run without Roslyn.</summary>
public sealed class FakeSymbolParser : ISymbolParser
{
    private readonly Dictionary<string, ParseOutcome> _outcomes = new(StringComparer.Ordinal);
    private readonly List<string> _paths = [];

    /// <summary>The version the fake reports; a test sets another to see a new parser version re-parse every file.</summary>
    public string Version { get; init; } = "fake-1";

    /// <summary>How many times <see cref="Parse"/> ran, so a test can see an unchanged file was not parsed again.</summary>
    public int Parsed { get; private set; }

    /// <summary>The paths <see cref="Parse"/> ran for, in order.</summary>
    public IReadOnlyList<string> ParsedPaths => _paths;

    /// <summary>Runs when a path is parsed (a test cancels from it).</summary>
    public Action<string>? OnParse { get; set; }

    /// <summary>Registers what a path parses to.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="outcome">Its outcome.</param>
    /// <returns>This fake.</returns>
    public FakeSymbolParser With(string path, ParseOutcome outcome)
    {
        _outcomes[path] = outcome;
        return this;
    }

    /// <inheritdoc />
    public bool Handles(string path) => _outcomes.ContainsKey(path);

    /// <inheritdoc />
    public Result<ParseOutcome> Parse(string path, string text, CancellationToken cancellationToken)
    {
        Parsed++;
        _paths.Add(path);
        OnParse?.Invoke(path);
        return _outcomes.TryGetValue(path, out var outcome) ? outcome : new ParseOutcome([]);
    }
}
