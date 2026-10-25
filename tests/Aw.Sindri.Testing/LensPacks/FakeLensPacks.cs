using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Testing.LensPacks;

/// <summary>An <see cref="ILensPacks"/> in memory: the packs it was given and the ids a test says the profile chose. Held to <see cref="LensPacksContract"/>.</summary>
public sealed class FakeLensPacks : ILensPacks
{
    private readonly Dictionary<string, LensPack> _packs = new(StringComparer.Ordinal);
    private readonly Dictionary<LensPackKind, string> _defaults = new() { [LensPackKind.Design] = "design-five", [LensPackKind.Review] = "code-review" };
    private readonly Dictionary<(LensPackKind, string), string> _repos = [];

    /// <summary>The (kind, repo) pairs asked for, in order.</summary>
    public IList<(LensPackKind Kind, RepoName? Repo)> Asked { get; } = [];

    /// <summary>Holds a pack.</summary>
    /// <param name="pack">The pack.</param>
    /// <returns>The same instance.</returns>
    public FakeLensPacks With(LensPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        _packs[pack.Id.Value] = pack;
        return this;
    }

    /// <summary>Sets the pack id the profile chooses for a loop.</summary>
    /// <param name="kind">The loop.</param>
    /// <param name="id">The pack id.</param>
    /// <returns>The same instance.</returns>
    public FakeLensPacks Choosing(LensPackKind kind, string id)
    {
        _defaults[kind] = id;
        return this;
    }

    /// <summary>Sets the pack id the profile chooses for a loop in one repo.</summary>
    /// <param name="kind">The loop.</param>
    /// <param name="repo">The repo.</param>
    /// <param name="id">The pack id.</param>
    /// <returns>The same instance.</returns>
    public FakeLensPacks ChoosingFor(LensPackKind kind, string repo, string id)
    {
        _repos[(kind, repo)] = id;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<LensPack>> Resolve(LensPackKind kind, RepoName? repo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Asked.Add((kind, repo));
        var id = repo is { } named && _repos.TryGetValue((kind, named.Value), out var own) ? own : _defaults[kind];
        return ValueTask.FromResult(Check(kind, id));
    }

    private Result<LensPack> Check(LensPackKind kind, string id)
    {
        if (!_packs.TryGetValue(id, out var pack))
        {
            return SindriErrors.InvalidInput.With($"the lens pack '{id}' is not a built-in pack");
        }

        if (pack.Applies != kind)
        {
            return SindriErrors.InvalidInput.With($"the lens pack '{id}' serves the {pack.Applies} loop, not the {kind} one");
        }

        return pack.Lenses.Count is > 0 and <= LensPack.MaxLenses
            ? pack
            : SindriErrors.InvalidInput.With($"the lens pack '{id}' has {pack.Lenses.Count} lenses, outside 1 to {LensPack.MaxLenses}");
    }
}
