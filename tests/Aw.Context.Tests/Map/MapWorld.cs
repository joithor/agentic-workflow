using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Context.Features;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.EvalProbes;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>
/// The map facade over the fakes, composed the way Sindri composes it (AddContextFeatures), with the fixture map placed at
/// <see cref="MapPath"/>. ValidateOnBuild is off: AddContextFeatures also registers the query slices, whose ports
/// (doc index, sources) these tests don't need; resolving IContextMap still checks what the map needs.
/// </summary>
internal sealed class MapWorld : IDisposable
{
    public const string MapPath = "/maps/product-map.yaml";

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public MapWorld()
    {
        Sources = new FixedSourceCatalog(Docs);
        Exports = new FakeExportStore(Clock);
        Reader.Put(MapPath, Maps.Fixture(), FixtureMap.Yaml);
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<IGraphStore>(Store)
            .AddSingleton<IExportStore>(Exports)
            .AddSingleton<IMapFileReader>(Reader)
            .AddSingleton<IScrubberFactory>(new FakeScrubberFactory())
            .AddSingleton<ISourceCatalog>(Sources)
            .AddSingleton<IDocIndex>(new FakeDocIndex(Clock))
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSingleton<IProbeFileReader>(new NoProbes())
            .AddSingleton<IProbeReportStore>(new NoProbes())
            .AddContextFeatures()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
        _scope = _provider.CreateScope();
    }

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    public FakeGraphStore Store { get; } = new();

    public FakeExportStore Exports { get; }

    public FakeMapFileReader Reader { get; } = new();

    public IContextMap Map => _scope.ServiceProvider.GetRequiredService<IContextMap>();

    /// <summary>The whole facade (S-3b: graph reads and queries over the imported map).</summary>
    public IContextEngine Engine => _scope.ServiceProvider.GetRequiredService<IContextEngine>();

    /// <summary>The sources the queries search: repo-docs over the fixture's docs by default.</summary>
    public FakeContextSource Docs { get; } = FakeContextSource.FixtureDocs();

    public FixedSourceCatalog Sources { get; }

    public static IReadOnlyList<ContextRepo> Repos { get; } = [new(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes"), new(ContextRepoName.From("other"), "/work/other")];

    public static ContextScope Human { get; } = new(Settings: null, Repos, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    public static ContextScope Session(string? repo, bool enabled = true)
    {
        using var section = System.Text.Json.JsonDocument.Parse(enabled ? """{"sessions":{"enabled":true}}""" : """{"sessions":{"enabled":false}}""");
        return new(section.RootElement.Clone(), Repos, new ContextCaller(ContextCallerKind.Session, repo is null ? null : ContextRepoName.From(repo), ContextClient.Http));
    }

    public static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>YAML from lines (one string per line).</summary>
    public static string Yaml(params string[] lines) => string.Join('\n', lines) + "\n";

    /// <summary>Imports a map written as YAML (placed at '/maps/other.yaml').</summary>
    public async Task<MapImported> ImportYaml(string yaml)
    {
        const string path = "/maps/other.yaml";
        Reader.Put(path, Maps.Parse(yaml), yaml);
        var preview = await Map.PreviewMap(path, Human, None);
        Assert.True(preview.IsSuccess, preview.IsError ? preview.Error.Message : null);
        var imported = await Map.ImportMap(path, preview.Value.Hash, Human, None);
        Assert.True(imported.IsSuccess, imported.IsError ? imported.Error.Message : null);
        return imported.Value;
    }

    /// <summary>Imports the fixture map with its own hash.</summary>
    public async Task<MapImported> ImportFixture()
    {
        var preview = await Map.PreviewMap(MapPath, Human, None);
        var imported = await Map.ImportMap(MapPath, preview.Value.Hash, Human, None);
        Assert.True(imported.IsSuccess, imported.IsError ? imported.Error.Message : null);
        return imported.Value;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
