using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Tests.Ids;

public sealed class MessageIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void Round_trips_through_the_Kernel_profile()
    {
        var id = MessageId.From("messageid-1");

        var back = AwJson.Deserialize<MessageIdHolder>(AwJson.Serialize(new MessageIdHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<MessageIdHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parses_valid_text_and_refuses_invalid_text()
    {
        Assert.True(MessageId.TryParse("messageid-1", provider: null, out _));
        Assert.False(MessageId.TryParse(" ", provider: null, out _));
    }
}
