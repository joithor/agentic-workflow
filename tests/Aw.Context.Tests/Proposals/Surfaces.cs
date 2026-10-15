using Aw.Context.Core.Graph;
using Aw.Context.Core.Proposals;

namespace Aw.Context.Tests.Proposals;

/// <summary>Builds the surfaces a scoping run proposes.</summary>
internal static class Surfaces
{
    public static ProposedSurface Of(string name, string? key = null, NodeKind? kind = null, string? parent = null) =>
        new(name, key is null ? null : NodeKey.From(key), kind, parent is null ? null : NodeKey.From(parent), Evidence: null, Quote: null);
}
