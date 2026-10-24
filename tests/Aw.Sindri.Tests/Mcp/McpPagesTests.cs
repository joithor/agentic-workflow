using System.Text.Json;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>One limit and offset for every list a tool returns. Buckets stay whole; their lists page.</summary>
public sealed class McpPagesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Each_list_keeps_one_window_and_the_buckets_stay_whole()
    {
        var answer = Json("{\"sections\":[{\"name\":\"graph\",\"items\":[\"g0\",\"g1\"],\"more\":[\"C3\"]},{\"name\":\"records\",\"items\":[\"r0\"],\"more\":[]}],\"groups\":[{\"group\":\"queued\",\"items\":[\"a\",\"b\",\"c\"]},{\"group\":\"running\",\"items\":[]}],\"edges\":[\"e0\",\"e1\",\"e2\"],\"counts\":{\"edges\":3}}");

        var page = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 0));

        Assert.Equal(["graph", "records"], Names(page, "sections"));
        Assert.Equal(["g0"], Items(page, "sections", 0));
        Assert.Equal(["queued", "running"], Names(page, "groups", "group"));
        Assert.Equal(["a"], Items(page, "groups", 0));
        Assert.Equal("e0", page.GetProperty("edges")[0].GetString());
        Assert.Equal(1, page.GetProperty("edges").GetArrayLength());
        Assert.Equal(3, page.GetProperty("counts").GetProperty("edges").GetInt32());
        Assert.Equal((0, 1, true), (page.GetProperty("page").GetProperty("offset").GetInt32(), page.GetProperty("page").GetProperty("limit").GetInt32(), page.GetProperty("page").GetProperty("more").GetBoolean()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_next_offset_continues_and_an_offset_past_the_end_is_empty()
    {
        var answer = Json("""{ "edges": ["e0", "e1", "e2"] }""");

        var second = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 1));
        var past = McpPages.Apply(answer, new McpWindow(Limit: 1, Offset: 5));

        Assert.Equal("e1", second.GetProperty("edges")[0].GetString());
        Assert.True(second.GetProperty("page").GetProperty("more").GetBoolean());
        Assert.Equal(0, past.GetProperty("edges").GetArrayLength());
        Assert.False(past.GetProperty("page").GetProperty("more").GetBoolean());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_is_a_limit_of_1_or_more_and_an_offset_of_0_or_more()
    {
        Assert.Null(McpArguments.ReadPage(arguments: null).Value);
        Assert.Equal(new McpWindow(20, 0), McpArguments.ReadPage(Args("""{"limit":20}""")).Value);
        Assert.Equal(new McpWindow(20, 3), McpArguments.ReadPage(Args("""{"limit":20,"offset":3}""")).Value);

        Assert.Equal("SND-CONTEXT-006", McpArguments.ReadPage(Args("""{"offset":1}""")).Error.Code);
        Assert.Equal("SND-CONTEXT-006", McpArguments.ReadPage(Args("""{"limit":0}""")).Error.Code);
        Assert.Equal("SND-CONTEXT-022", McpArguments.ReadPage(Args("""{"limit":"2"}""")).Error.Code);
    }

    private static string[] Names(JsonElement page, string list, string property = "name") =>
        [.. page.GetProperty(list).EnumerateArray().Select(item => item.GetProperty(property).GetString()!)];

    private static string[] Items(JsonElement page, string list, int index) =>
        [.. page.GetProperty(list)[index].GetProperty("items").EnumerateArray().Select(item => item.GetString()!)];

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static Dictionary<string, JsonElement> Args(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
}
