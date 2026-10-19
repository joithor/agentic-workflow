namespace Aw.Context.Core.Code;

/// <summary>A test file the lexical rules tied to implementing files.</summary>
/// <param name="Path">The test file.</param>
/// <param name="Basis">Imports or sibling.</param>
/// <param name="Named">The implementing files it names.</param>
public sealed record LexicalTest(string Path, CodeBasis Basis, IReadOnlyList<string> Named);
