using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory <see cref="IContextEngine"/>, for callers (Sindri's tests) that don't query context: it checks settings with the engine's own
/// pure rules (SettingsReader, ScopeRules for the repos) and the extra scrub patterns with <see cref="FakeScrubberFactory"/>, and answers every query or refresh with SND-CONTEXT-011. It
/// passes ContextEngineContract. The real engine and its fakes
/// are tested in Aw.Context.Tests.
/// </summary>
public sealed class FakeContextEngine : IContextEngine
{
    private readonly FakeScrubberFactory _scrubbers = new();

    private FakeContextEngine()
    {
    }

    /// <summary>The one instance.</summary>
    public static FakeContextEngine Instance { get; } = new();

    /// <inheritdoc />
    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos) =>
        SettingsReader.Read(section).Bind(read => _scrubbers.For(read.ExtraPatterns)).Bind(_ => SettingsReader.Check(section, repos));   // the keys, then the patterns, then the repos

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

    /// <summary>No probe file: a run answers SND-CONTEXT-036.</summary>
    public ValueTask<Result<ProbeRun>> EvalProbes(string? file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ProbeRun>>(ContextErrors.NoProbeFile.With("this test engine has no probe file"));

    /// <inheritdoc/>
    public ValueTask<Result<NodeView>> ShowNode(string key, ContextScope scope, CancellationToken cancellationToken) => NoMap<NodeView>();

    /// <inheritdoc/>
    public ValueTask<Result<NeighborsView>> Neighbors(string key, int? depth, ContextScope scope, CancellationToken cancellationToken) => NoMap<NeighborsView>();

    /// <inheritdoc/>
    public ValueTask<Result<PathView>> Path(string from, string to, ContextScope scope, CancellationToken cancellationToken) => NoMap<PathView>();

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
