using System.Text.Json;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Testing.Map;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Map;

/// <summary>Parses map YAML the way the file reader does (YamlToJson, then AwJson).</summary>
internal static class Maps
{
    public static JsonSerializerOptions Json => FixtureMap.Json;

    public static MapDocument Parse(string yaml)
    {
        var problem = YamlToJson.TryConvert(yaml, out var json);
        Assert.Null(problem);
        var read = AwJson.Deserialize<MapDocument>(json, Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        return read.Value;
    }

    public static MapDocument Fixture() => Parse(FixtureMap.Yaml);

    public static MapImport Checked(MapDocument document)
    {
        var check = MapDocumentValidator.Check(document, KeyMigrations.Shipped);
        Assert.True(check.IsSuccess, check.IsError ? check.Error.Message : null);
        return check.Value;
    }

    public static NodeKey Key(string value) => NodeKey.From(value);
}
