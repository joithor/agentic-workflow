using System.Text;
using Aw.Context.Core.Eval;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Eval;

public sealed class ProbeIdTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_probe_id_round_trips_through_the_Kernel_profile()
    {
        var probe = Answers.Probe("orientation-cascade");

        var back = AwJson.Deserialize<ProbeCase>(AwJson.Serialize(probe, EvalJson.Options), EvalJson.Options);

        Assert.True(back.IsSuccess);
        Assert.Equal(probe.Id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("bad\u0001id")]
    public void A_malformed_probe_id_is_AW_KERNEL_001(string id)
    {
        var json = Encoding.UTF8.GetBytes("{\"id\":" + System.Text.Json.JsonSerializer.Serialize(id) + ",\"question\":\"q\",\"expect\":{}}");

        var read = AwJson.Deserialize<ProbeCase>(json, EvalJson.Options);

        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_probe_id_over_100_characters_is_refused() => Assert.False(ProbeId.TryFrom(new string('a', 101), out _));
}
