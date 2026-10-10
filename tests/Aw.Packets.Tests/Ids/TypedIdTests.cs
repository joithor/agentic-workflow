using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core.Ids;

namespace Aw.Packets.Tests.Ids;

/// <summary>Every Packets id round-trips through the Kernel profile; invalid, missing or null is AW-KERNEL-001 (R9).</summary>
public sealed class TypedIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    public static TheoryData<string> Kinds => ["packet", "task", "workstream", "question", "example"];

    private static Result<string> RoundTrip(string kind, string value) => kind switch
    {
        "packet" => Through(new PacketIdHolder(PacketId.From(value)), static h => h.Id.Value),
        "task" => Through(new TaskIdHolder(TaskId.From(value)), static h => h.Id.Value),
        "workstream" => Through(new WorkstreamIdHolder(WorkstreamId.From(value)), static h => h.Id.Value),
        "question" => Through(new QuestionIdHolder(QuestionId.From(value)), static h => h.Id.Value),
        _ => Through(new ExampleIdHolder(ExampleId.From(value)), static h => h.Id.Value),
    };

    private static Result<string> Through<T>(T holder, Func<T, string> value) =>
        AwJson.Deserialize<T>(AwJson.Serialize(holder, _json), _json).Map(value);

    private static Result<string> Read(string kind, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return kind switch
        {
            "packet" => AwJson.Deserialize<PacketIdHolder>(bytes, _json).Map(static h => h.Id.Value),
            "task" => AwJson.Deserialize<TaskIdHolder>(bytes, _json).Map(static h => h.Id.Value),
            "workstream" => AwJson.Deserialize<WorkstreamIdHolder>(bytes, _json).Map(static h => h.Id.Value),
            "question" => AwJson.Deserialize<QuestionIdHolder>(bytes, _json).Map(static h => h.Id.Value),
            _ => AwJson.Deserialize<ExampleIdHolder>(bytes, _json).Map(static h => h.Id.Value),
        };
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public void Round_trips_through_the_Kernel_profile(string kind) =>
        Assert.Equal("ok-1", RoundTrip(kind, "ok-1").Value);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public void Blank_missing_or_null_is_AW_KERNEL_001(string kind)
    {
        foreach (var json in (string[])["{\"id\":\" \"}", "{}", "{\"id\":null}"])
        {
            Assert.Equal(KernelErrors.Invalid.Code, Read(kind, json).Error.Code);
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("demo-packet", true)]
    [InlineData("a", true)]
    [InlineData("Demo", false)]
    [InlineData("-lead", false)]
    [InlineData("has space", false)]
    [InlineData("../up", false)]
    public void A_packet_id_is_safe_as_a_directory_name(string value, bool valid) =>
        Assert.Equal(valid, PacketId.TryParse(value, provider: null, out _));

    [Fact, Trait("Lane", "unit")]
    public void A_packet_id_is_at_most_64_characters()
    {
        Assert.True(PacketId.TryParse(new string('a', 64), provider: null, out _));
        Assert.False(PacketId.TryParse(new string('a', 65), provider: null, out _));
    }
}
