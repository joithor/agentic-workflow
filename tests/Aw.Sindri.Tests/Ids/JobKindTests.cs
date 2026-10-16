using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class JobKindTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void JobKind_round_trips_json()
    {
        var id = JobKind.From("RunService");

        var back = AwJson.Deserialize<JobKindHolder>(AwJson.Serialize(new JobKindHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"run-service\"}")]
    [InlineData("{\"id\":\"Run Service\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<JobKindHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parses_valid_text_and_refuses_invalid_text()
    {
        Assert.True(JobKind.TryParse("RunService", provider: null, out _));
        Assert.False(JobKind.TryParse("run-service", provider: null, out _));
    }
}
