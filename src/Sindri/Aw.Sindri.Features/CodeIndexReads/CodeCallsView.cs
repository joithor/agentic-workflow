using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// The answer to callers and callees (GET /sindri/code-index/callers, /callees): the facade's answer with a coverage per row and for the whole answer
/// (partial when any row is or the symbol's language is not C#, spec §5.5). An empty partial answer says 'none found' in <see cref="Note"/>, never 'none'.
/// </summary>
/// <param name="Stamp">Which index answered.</param>
/// <param name="Subject">The symbol asked about.</param>
/// <param name="Coverage">Partial when any row is partial, else full.</param>
/// <param name="Note">A sentence for a reader: how complete the rows are, and 'none found' for an empty partial answer.</param>
/// <param name="Edges">The rows.</param>
/// <param name="UnresolvedCount">Calls of the symbol that resolved to nothing.</param>
/// <param name="Truncated">True when a level had more than were returned.</param>
public sealed record CodeCallsView(AnswerStamp Stamp, SymbolRef Subject, CallCoverage Coverage, string Note, IReadOnlyList<CodeCallRow> Edges, int UnresolvedCount, bool Truncated)
{
    /// <summary>The view of a facade answer.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The view.</returns>
    internal static CodeCallsView From(CallsAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var rows = answer.Edges.Select(CodeCallRow.From).ToList();
        var partial = rows.Exists(static r => r.Coverage == CallCoverage.Partial) || !IsParsed(answer.Subject);
        var coverage = partial ? CallCoverage.Partial : CallCoverage.Full;
        return new(answer.Stamp, answer.Subject, coverage, NoteFor(coverage, rows.Count), rows, answer.UnresolvedCount, answer.Truncated);
    }

    // Only C# has the parser's own call sites (spec §5.5); every other language's edges are graphify's, so even an empty answer is partial.
    private static bool IsParsed(SymbolRef symbol)
    {
        var text = symbol.Value;
        var path = text[(text.IndexOf(':', StringComparison.Ordinal) + 1)..];
        path = path[..Math.Max(path.IndexOf('#', StringComparison.Ordinal), 0)];
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string NoteFor(CallCoverage coverage, int rows) => (coverage, rows) switch
    {
        (CallCoverage.Partial, 0) => "none found (coverage partial: graphify keeps one call site per caller and callee pair)",
        (CallCoverage.Partial, _) => "coverage partial: rows from graphify may miss other call sites",
        (_, 0) => "none",
        _ => "coverage full",
    };
}
