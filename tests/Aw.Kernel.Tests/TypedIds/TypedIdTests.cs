using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;

namespace Aw.Kernel.Tests.TypedIds;

[Trait("Lane", "unit")]
public sealed class TypedIdTests
{
    private static Guid RunGuid { get; } = new(0x0192a3b4u, 0xc5d6, 0x7e8f, 0x90, 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde);

    private static System.Text.Json.JsonSerializerOptions Options { get; } = AwJson.CreateOptions(TypedIdJsonContext.Default);

    private static TypedIdPayload Sample(string item = "item-1") =>
        new(ItemId.From(item), RunId.From(RunGuid), TokenBudget.From(1200), "n");

    [Fact]
    public void Typed_ids_write_as_their_value_never_as_an_empty_object()
    {
        var json = Encoding.UTF8.GetString(AwJson.Serialize(Sample(), Options));

        Assert.Equal("{\"item\":\"item-1\",\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":1200,\"note\":\"n\"}", json);
        Assert.DoesNotContain("{}", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Typed_ids_round_trip_through_the_kernel_json_profile()
    {
        var bytes = AwJson.Serialize(Sample(), Options);

        var read = AwJson.Deserialize<TypedIdPayload>(bytes, Options);

        Assert.True(read.IsSuccess);
        Assert.Equal(Sample(), read.Value);
    }

    [Fact]
    public void A_string_id_writes_exactly_as_the_profile_writes_the_plain_string()
    {
        const string text = "it's <a>&\"x\"\u2028";

        Assert.Equal(AwJson.Serialize(text, Options), AwJson.Serialize(ItemId.From(text), Options));
    }

    [Fact]
    public void A_guid_id_is_written_in_its_one_canonical_form_lowercase_d()
    {
        var json = Encoding.UTF8.GetString(AwJson.Serialize(Sample(), Options));

        Assert.Contains("\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void An_id_value_the_type_rejects_is_an_invalid_input_error_not_an_exception()
    {
        var read = AwJson.Deserialize<TypedIdPayload>(
            "{\"item\":\" \",\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":1,\"note\":\"n\"}"u8, Options);

        Assert.True(read.IsError);
        Assert.Equal("AW-KERNEL-001", read.Error.Code);
        Assert.Contains("ItemId", read.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"item\":7,\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":1,\"note\":\"n\"}")]
    [InlineData("{\"item\":\"a\",\"run\":\"not-a-guid\",\"budget\":1,\"note\":\"n\"}")]
    [InlineData("{\"item\":\"a\",\"run\":\"00000000-0000-0000-0000-000000000000\",\"budget\":1,\"note\":\"n\"}")]
    [InlineData("{\"item\":\"a\",\"run\":\"0192A3B4-C5D6-7E8F-9012-3456789ABCDE\",\"budget\":1,\"note\":\"n\"}")]
    [InlineData("{\"item\":\"a\",\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":\"1\",\"note\":\"n\"}")]
    [InlineData("{\"item\":\"a\",\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":-1,\"note\":\"n\"}")]
    public void A_wrong_token_or_rejected_value_for_any_id_kind_is_an_invalid_input_error(string json)
    {
        var read = AwJson.Deserialize<TypedIdPayload>(Encoding.UTF8.GetBytes(json), Options);

        Assert.True(read.IsError);
        Assert.Equal("AW-KERNEL-001", read.Error.Code);
    }

    [Theory]
    [InlineData("{\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":1,\"note\":\"n\"}", "item")]
    [InlineData("{\"item\":\"a\",\"budget\":1,\"note\":\"n\"}", "run")]
    [InlineData("{\"item\":\"a\",\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"note\":\"n\"}", "budget")]
    public void A_missing_id_property_is_an_invalid_input_error_never_an_uninitialized_id(string json, string missing)
    {
        var read = AwJson.Deserialize<TypedIdPayload>(Encoding.UTF8.GetBytes(json), Options);

        Assert.True(read.IsError);
        Assert.Equal("AW-KERNEL-001", read.Error.Code);
        Assert.Contains(missing, read.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"item\":null,\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":1,\"note\":\"n\"}")]
    [InlineData("{\"item\":\"\",\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":1,\"note\":\"n\"}")]
    [InlineData("{\"item\":\"a\",\"run\":null,\"budget\":1,\"note\":\"n\"}")]
    [InlineData("{\"item\":\"a\",\"run\":\"\",\"budget\":1,\"note\":\"n\"}")]
    [InlineData("{\"item\":\"a\",\"run\":\"0192a3b4-c5d6-7e8f-9012-3456789abcde\",\"budget\":null,\"note\":\"n\"}")]
    public void A_null_or_empty_id_is_an_invalid_input_error(string json)
    {
        var read = AwJson.Deserialize<TypedIdPayload>(Encoding.UTF8.GetBytes(json), Options);

        Assert.True(read.IsError);
        Assert.Equal("AW-KERNEL-001", read.Error.Code);
    }

    [Fact]
    public void Ids_bind_from_route_text_through_their_parse_methods_and_refuse_bad_text()
    {
        Assert.True(Bind<ItemId>("item-1", out var item));
        Assert.Equal(ItemId.From("item-1"), item);
        Assert.True(Bind<RunId>("0192a3b4-c5d6-7e8f-9012-3456789abcde", out var run));
        Assert.Equal(RunId.From(RunGuid), run);
        Assert.True(Bind<TokenBudget>("1200", out var budget));
        Assert.Equal(TokenBudget.From(1200), budget);

        Assert.False(Bind<ItemId>(" ", out _));
        Assert.False(Bind<RunId>("00000000-0000-0000-0000-000000000000", out _));
        Assert.False(Bind<RunId>("nope", out _));
        Assert.False(Bind<TokenBudget>("-1", out _));
    }

    [Fact]
    public void Ids_as_dictionary_keys_write_like_plain_keys_and_read_back()
    {
        var maps = new IdMaps(
            new Dictionary<ItemId, int> { [ItemId.From("it's <a>&")] = 1 },
            new Dictionary<RunId, int> { [RunId.From(RunGuid)] = 2 },
            new Dictionary<TokenBudget, int> { [TokenBudget.From(1200)] = 3 });

        var bytes = AwJson.Serialize(maps, Options);
        var read = AwJson.Deserialize<IdMaps>(bytes, Options);

        var plain = new PlainMaps(
            new Dictionary<string, int>(StringComparer.Ordinal) { ["it's <a>&"] = 1 },
            new Dictionary<string, int>(StringComparer.Ordinal) { ["0192a3b4-c5d6-7e8f-9012-3456789abcde"] = 2 },
            new Dictionary<string, int>(StringComparer.Ordinal) { ["1200"] = 3 });
        Assert.Equal(AwJson.Serialize(plain, Options), bytes);
        Assert.True(read.IsSuccess);
        Assert.Equal(1, read.Value.Items[ItemId.From("it's <a>&")]);
        Assert.Equal(2, read.Value.Runs[RunId.From(RunGuid)]);
        Assert.Equal(3, read.Value.Budgets[TokenBudget.From(1200)]);
    }

    [Theory]
    [InlineData("{\"items\":{\" \":1},\"runs\":{},\"budgets\":{}}")]
    [InlineData("{\"items\":{},\"runs\":{\"0192A3B4-C5D6-7E8F-9012-3456789ABCDE\":1},\"budgets\":{}}")]
    [InlineData("{\"items\":{},\"runs\":{\"nope\":1},\"budgets\":{}}")]
    [InlineData("{\"items\":{},\"runs\":{},\"budgets\":{\"-1\":1}}")]
    [InlineData("{\"items\":{},\"runs\":{},\"budgets\":{\"01\":1}}")]
    [InlineData("{\"items\":{},\"runs\":{},\"budgets\":{\"1.5\":1}}")]
    public void A_dictionary_key_the_id_type_refuses_is_an_invalid_input_error(string json)
    {
        var read = AwJson.Deserialize<IdMaps>(Encoding.UTF8.GetBytes(json), Options);

        Assert.True(read.IsError);
        Assert.Equal("AW-KERNEL-001", read.Error.Code);
    }

    [Fact]
    public void A_typed_id_comes_from_the_id_port_never_from_guid_new_guid()
    {
        var generator = new FixedIds(RunGuid);

        var run = generator.New<RunId>();

        Assert.Equal(RunId.From(RunGuid), run);
    }

    [Fact]
    public void An_id_port_that_hands_out_a_value_the_type_rejects_is_a_bug()
    {
        var generator = new FixedIds(Guid.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() => generator.New<RunId>());

        Assert.Contains("RunId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_typed_id_round_trips_through_a_data_reader_by_its_value()
    {
        using var table = new DataTable();
        _ = table.Columns.Add("item", typeof(string));
        _ = table.Columns.Add("run", typeof(Guid));
        _ = table.Columns.Add("budget", typeof(int));
        var sample = Sample();
        _ = table.Rows.Add(sample.Item.Value, sample.Run.Value, sample.Budget.Value);

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        Assert.True(ItemId.TryFrom(reader.GetString(0), out var item));
        Assert.True(RunId.TryFrom(reader.GetGuid(1), out var run));
        Assert.True(TokenBudget.TryFrom(reader.GetInt32(2), out var budget));
        Assert.Equal(sample, new TypedIdPayload(item, run, budget, "n"));
    }

    /// <summary>The call the request-delegate generator makes for a route parameter: IParsable&lt;T&gt;.TryParse.</summary>
    private static bool Bind<TId>(string text, [MaybeNullWhen(false)] out TId id)
        where TId : IParsable<TId> => TId.TryParse(text, provider: null, out id);

    private sealed class FixedIds(Guid id) : IIdGenerator
    {
        public Guid NewId() => id;
    }
}
