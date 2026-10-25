using Aw.Context.Core.Lenses;

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
    bool SearchedFilesCapped)
{
    /// <summary>True when the search found more candidate test files than it read, so the list of tests is not the whole answer.</summary>
    public bool TestsTruncated { get; init; }

    /// <summary>True when more schema files matched than were listed, or more things were searchable than were searched.</summary>
    public bool SchemaTruncated { get; init; }

    /// <summary>The 'lexical-tests' row of 'searched.sources'; null when the lexical step did not run.</summary>
    public SearchedSource? TestsRow { get; init; }

    /// <summary>The 'lexical-schema' row of 'searched.sources'; null when the lexical step did not run.</summary>
    public SearchedSource? SchemaRow { get; init; }
}
