namespace Aw.Context.Core.Code;

/// <summary>What the lexical search found, for <see cref="CodeLensMerge"/>.</summary>
/// <param name="Tests">Test files tied to implementing files by <see cref="CodeBasis.Imports"/> or <see cref="CodeBasis.Sibling"/>.</param>
/// <param name="SchemaFiles">Schema files that name a thing.</param>
/// <param name="Searched">What was read per repo.</param>
/// <param name="Unlocated">Implementing files with no repo evidence.</param>
/// <param name="SearchedFilesCapped">True when more implementing files were found than were searched.</param>
public sealed record CodeLexicalFindings(
    IReadOnlyList<LexicalTest> Tests,
    IReadOnlyList<LexicalSchemaFile> SchemaFiles,
    IReadOnlyList<CodeLexical> Searched,
    int Unlocated,
    bool SearchedFilesCapped);
