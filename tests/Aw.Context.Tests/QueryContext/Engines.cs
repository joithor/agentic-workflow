using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Sources;
using Aw.Context.Features;
using Aw.Context.Features.QueryContext;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.SourcesStatus;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The facade over fakes, and the scopes the tests ask in.</summary>
internal static class Engines
{
    internal static readonly ContextRepo[] Repos = [new("split-panes", "/repos/split-panes"), new("other", "/repos/other")];

    internal static IContextEngine Over(FixedSourceCatalog catalog, FakeDocIndex? index = null)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        var scrubber = new FakeScrubber();
        var docs = index ?? new FakeDocIndex(clock);
        return new ContextEngine(new QueryContextService(catalog, scrubber, scrubber, new SequentialIdGenerator(), clock),
            new SourcesStatusService(docs, catalog, clock), new RefreshDocsService(docs));
    }

    internal static IContextEngine Over(params IContextSource[] sources) => Over(new FixedSourceCatalog(sources));

    internal static ContextScope Human(string? settingsJson = null) =>
        new(Settings(settingsJson), Repos, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    internal static ContextScope Session(string? repo, string? settingsJson = null) =>
        new(Settings(settingsJson), Repos, new ContextCaller(ContextCallerKind.Session, repo, ContextClient.Http));

    private static JsonElement? Settings(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
