namespace Aw.Context.Core.Code;

/// <summary>Adds the code index's findings to a code view (S-9a): pure.</summary>
public static class CodeLensSymbols
{
    /// <summary>Fills callers and callees and replaces the 'code-index' row; a degraded row also lands in <c>degraded</c>.</summary>
    /// <param name="view">The view with the graph and lexical halves.</param>
    /// <param name="findings">What the code index gave.</param>
    /// <returns>The view.</returns>
    public static CodeView With(CodeView view, CodeSymbolFindings findings) => throw new NotSupportedException();
}
