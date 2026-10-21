using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>Adds the code index's findings to a code view (S-9a): pure.</summary>
public static class CodeLensSymbols
{
    /// <summary>Fills callers and callees and replaces the 'code-index' row; a degraded row also lands in 'degraded'.</summary>
    /// <param name="view">The view with the graph and lexical halves.</param>
    /// <param name="findings">What the code index gave.</param>
    /// <returns>The view.</returns>
    public static CodeView With(CodeView view, CodeSymbolFindings findings)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(findings);
        var row = findings.Row;
        return view with
        {
            Callers = findings.Callers,
            Callees = findings.Callees,
            Searched = view.Searched with { Sources = [.. view.Searched.Sources.Select(s => string.Equals(s.Id.Value, row.Id.Value, StringComparison.Ordinal) ? row : s)] },
            Degraded = row.State == SearchedState.Degraded ? [.. view.Degraded, $"{row.Id.Value}: {row.Code ?? row.Reason ?? "degraded"}"] : view.Degraded,
        };
    }
}
