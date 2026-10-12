using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Features;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>The map facade over the fakes, composed the way Sindri composes it (AddContextFeatures), with the fixture map placed at <see cref="MapPath"/>.</summary>
internal sealed class MapWorld : IDisposable
{
    public const string MapPath = "/maps/product-map.yaml";

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public MapWorld()
    {
        Exports = new FakeExportStore(Clock);
        Reader.Put(MapPath, Maps.Fixture(), FixtureMap.Yaml);
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<IGraphStore>(Store)
            .AddSingleton<IExportStore>(Exports)
            .AddSingleton<IMapFileReader>(Reader)
            .AddContextFeatures()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _scope = _provider.CreateScope();
    }

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    public FakeGraphStore Store { get; } = new();

    public FakeExportStore Exports { get; }

    public FakeMapFileReader Reader { get; } = new();

    public IContextMap Map => _scope.ServiceProvider.GetRequiredService<IContextMap>();

    public static MapReader Human { get; } = new(new ContextSettings(), new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    public static MapReader Session(string? repo, bool enabled = true) =>
        new(new ContextSettings { SessionsEnabled = enabled }, new ContextCaller(ContextCallerKind.Session, repo, ContextClient.Http));

    public static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Imports the fixture map with its own hash.</summary>
    public async Task<MapImported> ImportFixture()
    {
        var preview = await Map.PreviewMap(MapPath, None);
        var imported = await Map.ImportMap(MapPath, preview.Value.Hash, None);
        Assert.True(imported.IsSuccess, imported.IsError ? imported.Error.Message : null);
        return imported.Value;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
