using System.Text;
using System.Text.Json;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Tests.Ids;

/// <summary>Every Evidence id round-trips through the Kernel profile; blank, missing or null is AW-KERNEL-001 (R9).</summary>
public sealed class TypedIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    private static Result<string> Read(string kind, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return kind switch
        {
            "run" => AwJson.Deserialize<RunIdHolder>(bytes, _json).Map(static h => h.Id.Value),
            "test" => AwJson.Deserialize<TestIdHolder>(bytes, _json).Map(static h => h.Id.Value),
            _ => AwJson.Deserialize<AttachmentIdHolder>(bytes, _json).Map(static h => h.Id.Value),
        };
    }

    private static string Write(string kind, string value) => Encoding.UTF8.GetString(kind switch
    {
        "run" => AwJson.Serialize(new RunIdHolder(RunId.From(value)), _json),
        "test" => AwJson.Serialize(new TestIdHolder(TestId.From(value)), _json),
        _ => AwJson.Serialize(new AttachmentIdHolder(AttachmentId.From(value)), _json),
    });

    [Theory, Trait("Lane", "unit")]
    [InlineData("run")]
    [InlineData("test")]
    [InlineData("attachment")]
    public void An_id_round_trips_as_a_plain_string(string kind)
    {
        var json = Write(kind, "save-btn_2");

        Assert.Equal("""{"id":"save-btn_2"}""", json);
        Assert.Equal("save-btn_2", Read(kind, json).Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("run", """{"id":""}""")]
    [InlineData("test", """{"id":"  "}""")]
    [InlineData("attachment", """{"id":null}""")]
    [InlineData("run", """{}""")]
    [InlineData("test", """{"id":4}""")]
    public void A_blank_missing_null_or_wrong_typed_id_is_refused(string kind, string json) =>
        Assert.Equal("AW-KERNEL-001", Read(kind, json).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_blank_id_cannot_be_made_in_code() =>
        Assert.False(TestId.TryFrom(" ", out _));
}
