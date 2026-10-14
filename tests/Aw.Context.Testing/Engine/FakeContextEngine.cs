using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
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

    private static Error Down() => ContextErrors.AllSourcesUnavailable.With("this test engine answers settings checks only");
}
