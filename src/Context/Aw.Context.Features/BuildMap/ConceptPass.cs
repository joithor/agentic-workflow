using Aw.Context.Core.Engine;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;

namespace Aw.Context.Features.BuildMap;

/// <summary>The concept step of a map build: reads the symbol vectors of the features' files, builds the glossary, derives the concepts with it and names them.</summary>
/// <param name="namer">Names the derived concepts.</param>
/// <param name="vectors">The stored symbol embeddings.</param>
internal sealed class ConceptPass(IConceptNamer namer, ISymbolVectors vectors)
{
    internal ValueTask<ConceptOutcome> Run(ContextSettings settings, GraphSnapshot joined, IReadOnlyList<ConceptPin> pins, IReadOnlyList<ContextRepo> repos, CancellationToken cancellationToken)
    {
        _ = (namer, vectors);
        throw new NotSupportedException();
    }
}
