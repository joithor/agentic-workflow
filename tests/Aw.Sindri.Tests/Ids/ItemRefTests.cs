using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class ItemRefTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void An_item_id_reads_as_an_id()
    {
        Assert.True(ItemRef.TryParse("ITM-0192f0c1a2b37c4d8e9f00112233aabb", out var value));

        Assert.Equal(ItemId.From("ITM-0192f0c1a2b37c4d8e9f00112233aabb"), value.Id);
        Assert.Null(value.Alias);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tracker_key_reads_as_an_alias()
    {
        Assert.True(ItemRef.TryParse("ENG-142", out var value));

        Assert.Null(value.Id);
        Assert.Equal(ItemAlias.From("ENG-142"), value.Alias);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("itm-142")]
    [InlineData("ITM-xyz")]
    [InlineData("../etc")]
    public void Anything_else_is_refused(string? text) => Assert.False(ItemRef.TryParse(text, out _));

    [Fact, Trait("Lane", "unit")]
    public void Round_trips_as_its_text()
    {
        var holder = new ItemRefHolder(ItemRef.To(ItemAlias.From("ENG-142")));

        var json = Encoding.UTF8.GetString(AwJson.Serialize(holder, _json));
        var back = AwJson.Deserialize<ItemRefHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.Equal("{\"id\":\"ENG-142\"}", json);
        Assert.Equal(holder, back.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bad_value_is_AW_KERNEL_001()
    {
        var read = AwJson.Deserialize<ItemRefHolder>(Encoding.UTF8.GetBytes("{\"id\":\"nope\"}"), _json);

        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_item_id_is_ITM_and_the_ids_hex()
    {
        var id = ItemId.New(new SequentialIdGenerator());

        Assert.Matches("^ITM-[0-9a-f]{32}$", id.Value);
    }
}
