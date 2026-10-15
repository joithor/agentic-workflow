using System.Text;
using Aw.Kernel.Json;
using Aw.Meta.Core;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Config;

namespace Aw.Meta.Tests.Adapters;

public sealed class FileMetaConfigTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private (FileMetaConfig Config, string Path) Make(string json)
    {
        var path = Path.Combine(_dir.Path, "team.json");
        File.WriteAllText(path, json, Encoding.UTF8);
        return (new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default)), path);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Unknown_fields_are_ignored_and_optional_ones_read()
    {
        var (config, _) = Make(
            """{"team":"acme","repo":"/r","lanes":["a","b"],"prompts":"/p","futureField":{"x":1},"gate":{"always":[],"docsOnly":[],"scopes":{},"alsoNew":true},"mergeOrder":[{"before":1,"after":2,"why":"x"}]}""");

        var loaded = await config.Load(CancellationToken.None);

        Assert.True(loaded.IsSuccess, loaded.IsError ? loaded.Error.Message : string.Empty);
        Assert.Equal(("acme", "/p", "meta"), (loaded.Value.Team, loaded.Value.Prompts, loaded.Value.MetaLane));
        _ = Assert.Single(loaded.Value.MergeOrder);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not json")]
    [InlineData("{\"team\":\"acme\"}")]
    [InlineData("{\"team\":\"acme\",\"repo\":\"/r\",\"lanes\":\"a\"}")]
    [InlineData("[]")]
    public async Task A_config_that_does_not_parse_is_AW_META_003(string json)
    {
        var (config, _) = Make(json);

        var loaded = await config.Load(CancellationToken.None);

        Assert.True(loaded.IsError);
        Assert.Equal(MetaErrors.ConfigInvalid.Code, loaded.Error.Code);
    }

    public void Dispose() => _dir.Dispose();
}
