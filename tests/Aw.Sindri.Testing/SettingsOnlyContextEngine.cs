using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Sindri.Testing;

/// <summary>
/// An <see cref="IContextEngine"/> for Sindri tests that don't query context: it checks settings with the engine's own
/// pure rules (SettingsReader, ScopeRules for the repos), and answers every query or refresh with SND-CONTEXT-011. It
/// passes ContextEngineContract. The real engine and its fakes
/// are tested in Aw.Context.Tests.
/// </summary>
public sealed class SettingsOnlyContextEngine : IContextEngine
{
    private SettingsOnlyContextEngine()
    {
    }

    /// <summary>The one instance.</summary>
    public static SettingsOnlyContextEngine Instance { get; } = new();

    /// <inheritdoc />
    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos) =>
        SettingsReader.Read(section).Bind(settings => ScopeRules.CheckRepos(section is { ValueKind: not JsonValueKind.Null }, settings, repos));

    /// <inheritdoc />
    public ValueTask<Result<ContextResult>> Query(ContextQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ContextResult>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SourcesView>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<RefreshReport>>(Down());

    private static Error Down() => ContextErrors.AllSourcesUnavailable.With("this test engine answers settings checks only");
}
