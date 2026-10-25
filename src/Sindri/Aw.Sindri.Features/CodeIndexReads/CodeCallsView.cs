using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// The answer to callers and callees (GET /sindri/code-index/callers, /callees): the facade's answer with a coverage per row and for the whole answer
/// (the facade's rule, spec §5.5: partial when any row or the symbol's language is). An empty partial answer says 'none found' in <see cref="Note"/>, never 'none'.
/// </summary>
/// <param name="Stamp">Which index answered.</param>
/// <param name="Subject">The symbol asked about.</param>
/// <param name="Coverage">The facade's whole-answer coverage.</param>
/// <param name="Note">A sentence for a reader: how complete the rows are, and 'none found' for an empty partial answer.</param>
/// <param name="Edges">The rows.</param>
/// <param name="UnresolvedCount">Calls of the symbol that resolved to nothing.</param>
/// <param name="Truncated">True when a level had more than were returned.</param>
public sealed record CodeCallsView(AnswerStamp Stamp, SymbolRef Subject, Coverage Coverage, string Note, IReadOnlyList<CodeCallRow> Edges, int UnresolvedCount, bool Truncated)
{
    /// <summary>The view of a facade answer.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The view.</returns>
    internal static CodeCallsView From(CallsAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var rows = answer.Edges.Select(CodeCallRow.From).ToList();
        return new(answer.Stamp, answer.Subject, answer.Coverage, NoteFor(answer.Coverage, rows.Count), rows, answer.UnresolvedCount, answer.Truncated);
    }

    private static string NoteFor(Coverage coverage, int rows) => (coverage, rows) switch
    {
        (Coverage.Unavailable, _) => "coverage unavailable: the index does not measure this language",
        (Coverage.Partial, 0) => "none found (coverage partial: graphify keeps one call site per caller and callee pair)",
        (Coverage.Partial, _) => "coverage partial: rows from graphify may miss other call sites",
        (_, 0) => "none",
        _ => "coverage full",
    };
}
