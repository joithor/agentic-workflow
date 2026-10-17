using System.Text.Json;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>Binding the sindri_* tools' arguments (API P8 unit A): every refusal is SND-CONTEXT-022 with its fix.</summary>
public sealed class McpArgumentsTests
{
    private const string Code = "SND-CONTEXT-022";

    private static Dictionary<string, JsonElement> Args(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_argument_names_the_allowed_ones()
    {
        var refusal = McpArguments.Only("sindri_item_timeline", Args("""{"itemId":"ITM-1","limit":3}"""), "itemId", "after");

        Assert.NotNull(refusal);
        Assert.Equal(Code, refusal.Code);
        Assert.Contains("sindri_item_timeline has no argument 'limit'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("itemId, after", refusal.Message, StringComparison.Ordinal);
        Assert.Null(McpArguments.Only("sindri_item_timeline", Args("""{"itemId":"ITM-1"}"""), "itemId", "after"));
        Assert.Null(McpArguments.Only("sindri_item_timeline", arguments: null, "itemId", "after"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{}")]
    [InlineData("""{"itemId":null}""")]
    [InlineData("""{"itemId":""}""")]
    [InlineData("""{"itemId":"  "}""")]
    [InlineData("""{"itemId":7}""")]
    public void A_missing_required_argument_is_022_with_an_example_call(string json)
    {
        var bound = McpArguments.RequiredString("sindri_item", Args(json), "itemId", "ITM-0001");

        Assert.True(bound.IsError);
        Assert.Equal(Code, bound.Error.Code);
        Assert.Contains("sindri_item needs 'itemId'", bound.Error.Message, StringComparison.Ordinal);
        Assert.Contains("""{"itemId": "ITM-0001"}""", bound.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_required_string_is_read_as_sent()
    {
        var bound = McpArguments.RequiredString("sindri_item", Args("""{"itemId":" ITM-0001 "}"""), "itemId", "ITM-0001");

        Assert.Equal(" ITM-0001 ", bound.Value);
        Assert.True(McpArguments.RequiredString("sindri_item", arguments: null, name: "itemId", example: "ITM-0001").IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_null_optional_argument_counts_as_absent()
    {
        Assert.Null(McpArguments.OptionalString("sindri_pending_decisions", Args("""{"itemId":null}"""), "itemId").Value);
        Assert.Null(McpArguments.OptionalString("sindri_pending_decisions", arguments: null, "itemId").Value);
        Assert.Null(McpArguments.OptionalLong("sindri_item_timeline", Args("""{"after":null}"""), "after").Value);
        Assert.Equal("ITM-1", McpArguments.OptionalString("sindri_pending_decisions", Args("""{"itemId":"ITM-1"}"""), "itemId").Value);

        var wrong = McpArguments.OptionalString("sindri_pending_decisions", Args("""{"itemId":4}"""), "itemId");
        Assert.Equal(Code, wrong.Error.Code);
        Assert.Contains("'itemId' must be a string, got number", wrong.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"after":-1}""")]
    [InlineData("""{"after":1.5}""")]
    [InlineData("""{"after":"3"}""")]
    [InlineData("""{"after":true}""")]
    [InlineData("""{"after":99999999999999999999}""")]
    public void After_must_be_a_whole_number_of_zero_or_more(string json)
    {
        var bound = McpArguments.OptionalLong("sindri_item_timeline", Args(json), "after");

        Assert.Equal(Code, bound.Error.Code);
        Assert.Contains("'after' must be a whole number of 0 or more", bound.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_whole_number_binds_including_zero()
    {
        Assert.Equal(0L, McpArguments.OptionalLong("sindri_item_timeline", Args("""{"after":0}"""), "after").Value);
        Assert.Equal(42L, McpArguments.OptionalLong("sindri_item_timeline", Args("""{"after":42}"""), "after").Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Kind_binds_by_its_json_name_and_refuses_another_value_listing_the_four()
    {
        var info = SindriJsonContext.Default.RequestKind;

        Assert.Equal(RequestKind.AddTask, McpArguments.RequiredEnum("sindri_request", Args("""{"kind":"add-task"}"""), "kind", info).Value);
        Assert.Equal(RequestKind.Handback, McpArguments.RequiredEnum("sindri_request", Args("""{"kind":"handback"}"""), "kind", info).Value);

        foreach (var json in new[] { """{"kind":"decide"}""", """{"kind":"AddTask"}""", """{"kind":3}""", """{"kind":null}""", "{}" })
        {
            var bound = McpArguments.RequiredEnum("sindri_request", Args(json), "kind", info);
            Assert.Equal(Code, bound.Error.Code);
            Assert.Contains("rescope, add-task, note, handback", bound.Error.Message, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tool_that_takes_no_arguments_refuses_any()
    {
        var refusal = McpArguments.None("sindri_queue", Args("""{"repo":"alpha"}"""));

        Assert.NotNull(refusal);
        Assert.Equal(Code, refusal.Code);
        Assert.Contains("sindri_queue takes no arguments, got 'repo'", refusal.Message, StringComparison.Ordinal);
        Assert.Null(McpArguments.None("sindri_queue", Args("{}")));
        Assert.Null(McpArguments.None("sindri_queue", arguments: null));
    }
}
