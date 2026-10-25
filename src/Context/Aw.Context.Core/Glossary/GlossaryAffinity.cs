using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>How much two features' glossary terms overlap.</summary>
internal static class GlossaryAffinity
{
    internal static double Between(Glossary glossary, NodeKey a, NodeKey b) =>
        throw new NotSupportedException();
}
