using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Sindri.Testing;

/// <summary>
/// An <see cref="IContextEngine"/> for Sindri tests that don't query context: it checks settings with the engine's own
/// pure rules (SettingsReader), and answers every query or refresh with SND-CONTEXT-011. The real engine and its fakes
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
    public Result<ContextSettings> CheckSettings(JsonElement? section) => SettingsReader.Read(section);

    /// <inheritdoc />
    public ValueTask<Result<ContextResult>> Query(ContextQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ContextResult>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SourcesView>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<RefreshReport>>(Down());

    /// <summary>No map: every map call answers SND-CONTEXT-030.</summary>
    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken) => NoMap<MapFilePreview>();

    /// <inheritdoc/>
    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken) => NoMap<MapImported>();

    /// <inheritdoc/>
    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) => NoMap<MapExported>();

    /// <inheritdoc/>
    public ValueTask<Result<MapStatusView>> MapStatus(ContextScope scope, CancellationToken cancellationToken) => NoMap<MapStatusView>();

    /// <inheritdoc/>
    public ValueTask<Result<MapHealth>> CheckMap(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new MapHealth("context:map", MapHealthState.Warn, "no map yet", "sindri context map import <file>")));

    /// <inheritdoc/>
    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new MapHealth("context:retention", MapHealthState.Ok, "nothing to purge", Fix: null)));

    /// <inheritdoc/>
    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new PurgeReport(Exports: 0, Versions: 0)));

    private static ValueTask<Result<T>> NoMap<T>() => ValueTask.FromResult<Result<T>>(ContextErrors.NoMap.With("this test engine has no map"));

    private static Error Down() => ContextErrors.AllSourcesUnavailable.With("this test engine answers settings checks only");
}
