using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>A parser that answers what the test registered per path, so the build can run without Roslyn.</summary>
public sealed class FakeSymbolParser : ISymbolParser
{
    private readonly Dictionary<string, ParseOutcome> _outcomes = new(StringComparer.Ordinal);

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
        => _outcomes.TryGetValue(path, out var outcome) ? outcome : new ParseOutcome([]);
}
