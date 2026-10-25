namespace Aw.Context.Core.Glossary;

/// <summary>Builds a <see cref="Glossary"/> from feature vectors: pure and deterministic, no I/O.</summary>
internal static class GlossaryBuilder
{
    internal static Glossary Build(IReadOnlyList<FeatureVectors> features, GlossaryRules rules) =>
        throw new NotSupportedException();
}
