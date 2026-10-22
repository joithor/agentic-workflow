using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>Which of a repo's unavailable layers can be built now (spec §6.3): the lock and tool check the build does, no work.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Buildable">Layers that were unavailable for a fixable cause and are available now.</param>
public sealed record LayerProbe(RepoName Repo, IReadOnlyList<LayerName> Buildable);
