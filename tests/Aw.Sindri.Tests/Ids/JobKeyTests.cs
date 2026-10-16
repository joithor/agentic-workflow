using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class JobKeyTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void JobKey_round_trips_json()
    {
        var id = JobKey.From("heartbeat");

        var back = AwJson.Deserialize<JobKeyHolder>(AwJson.Serialize(new JobKeyHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"Heartbeat\"}")]
    [InlineData("{\"id\":\"bad key\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<JobKeyHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parses_valid_text_and_refuses_invalid_text()
    {
        Assert.True(JobKey.TryParse("heartbeat", provider: null, out _));
        Assert.False(JobKey.TryParse("Heartbeat", provider: null, out _));
    }
}
