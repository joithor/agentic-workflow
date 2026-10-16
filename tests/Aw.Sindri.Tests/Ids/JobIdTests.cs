using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class JobIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void JobId_round_trips_json()
    {
        var id = JobId.From("JOB-0192f0c1a2b37c4d8e9f00112233aabb");

        var back = AwJson.Deserialize<JobIdHolder>(AwJson.Serialize(new JobIdHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"JOB-0192F0C1A2B37C4D8E9F00112233AABB\"}")]
    [InlineData("{\"id\":\"REQ-0192f0c1a2b37c4d8e9f00112233aabb\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<JobIdHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parses_valid_text_and_refuses_invalid_text()
    {
        Assert.True(JobId.TryParse("JOB-0192f0c1a2b37c4d8e9f00112233aabb", provider: null, out _));
        Assert.False(JobId.TryParse("JOB-0192F0C1A2B37C4D8E9F00112233AABB", provider: null, out _));
    }
}
