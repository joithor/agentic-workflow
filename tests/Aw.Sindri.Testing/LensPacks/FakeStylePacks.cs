using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Testing.LensPacks;

/// <summary>An <see cref="IStylePacks"/> in memory: the packs it was given, and the id the profile chooses (default 'plain'). A test changes a pack's hash by putting another pack under the same id.</summary>
public sealed class FakeStylePacks : IStylePacks
{
    private readonly Dictionary<string, StylePack> _packs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _repos = new(StringComparer.Ordinal);
    private string _default = "plain";

    /// <summary>The repos asked for, in order.</summary>
    public IList<RepoName?> Asked { get; } = [];

    /// <summary>Holds a pack.</summary>
    /// <param name="pack">The pack.</param>
    /// <returns>The same instance.</returns>
    public FakeStylePacks With(StylePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        _packs[pack.Id.Value] = pack;
        return this;
    }

    /// <summary>Sets the pack id the profile chooses.</summary>
    /// <param name="id">The pack id.</param>
    /// <returns>The same instance.</returns>
    public FakeStylePacks Choosing(string id)
    {
        _default = id;
        return this;
    }

    /// <summary>Sets the pack id the profile chooses in one repo.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="id">The pack id.</param>
    /// <returns>The same instance.</returns>
    public FakeStylePacks ChoosingFor(string repo, string id)
    {
        _repos[repo] = id;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<StylePack>> Resolve(RepoName? repo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Asked.Add(repo);
        return ValueTask.FromResult(Find(LensPackId.From(repo is { } named && _repos.TryGetValue(named.Value, out var own) ? own : _default)));
    }

    /// <inheritdoc/>
    public Result<StylePack> Find(LensPackId id) =>
        _packs.TryGetValue(id.Value, out var pack) ? pack : SindriErrors.InvalidInput.With($"the style pack '{id.Value}' is not a built-in pack");
}
