using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IMetaConfig: the one fake, kept honest by MetaConfigContract.</summary>
public sealed class InMemoryMetaConfig : IMetaConfig
{
    private TeamConfig? _config;

    /// <inheritdoc />
    public ValueTask<Result<TeamConfig>> Load(CancellationToken cancellationToken) =>
        _config is { } config ? Done.Of(config) : Done.Failed<TeamConfig>(MetaErrors.NoTeamConfig.With("no team config; run aw-meta team-init"));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(TeamConfig config, CancellationToken cancellationToken)
    {
        _config = config;
        return Done.Unit();
    }
}
