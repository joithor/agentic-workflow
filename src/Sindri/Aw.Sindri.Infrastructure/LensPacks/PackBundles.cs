using Aw.Kernel;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary><see cref="IPackBundles"/> over the style and lens pack resolvers (line spec §5.1, §5.2, LN-WF5).</summary>
/// <param name="styles">Resolves the style pack.</param>
/// <param name="styleSource">The style packs' files.</param>
/// <param name="lenses">Resolves the lens pack.</param>
internal sealed class PackBundles(IStylePacks styles, IStylePackSource styleSource, ILensPacks lenses) : IPackBundles
{
    public ValueTask<Result<PackBundle>> Resolve(PackBundleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
