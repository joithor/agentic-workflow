namespace Aw.Context.Core.Glossary;

/// <summary>Picks the glossary terms that characterize a set of features.</summary>
internal static class GlossaryTermPicker
{
    /// <summary>The terms the most of the features carry, then the heaviest, then by name.</summary>
    internal static IReadOnlyList<string> Top(MapGlossary? glossary, IReadOnlyList<string> features, int take) =>
        throw new NotSupportedException();
}
