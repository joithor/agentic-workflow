namespace Aw.Context.Core.Code;

/// <summary>Adds the lexical findings to a graph-half code view (S-CL): pure.</summary>
public static class CodeLensMerge
{
    /// <summary>Adds lexical tests and schema files; an edge test is never replaced, imports outranks sibling.</summary>
    /// <param name="view">The graph-half view.</param>
    /// <param name="findings">The lexical findings.</param>
    /// <returns>The view with tests, schema files, testedBy, counts, truncation and the lexical rows filled.</returns>
    public static CodeView With(CodeView view, CodeLexicalFindings findings) => throw new NotSupportedException();
}
