namespace Aw.Context.Core.Glossary;

/// <summary>The terms each feature carries, indexed once so group-to-group overlap is cheap.</summary>
internal sealed class GlossaryTermIndex
{
    internal GlossaryTermIndex(MapGlossary glossary) => throw new NotSupportedException();

    /// <summary>The Jaccard overlap of the terms the two groups' features carry, 0 to 1; 0 when either carries none.</summary>
    internal double Between(IReadOnlyList<string> a, IReadOnlyList<string> b) => throw new NotSupportedException();
}
