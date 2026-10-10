using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Values;

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

    [Theory, Trait("Lane", "unit")]
    [InlineData("owner/name", true)]
    [InlineData("my-org/my.repo_2", true)]
    [InlineData("name", false)]
    [InlineData("a/b/c", false)]
    [InlineData("owner/ name", false)]
    public void A_repo_slug_is_owner_and_name(string value, bool valid) =>
        Assert.Equal(valid, RepoSlug.TryParse(value, provider: null, out _));

    [Theory, Trait("Lane", "unit")]
    [InlineData(40, 'a', true)]
    [InlineData(64, '0', true)]
    [InlineData(12, 'a', false)]
    [InlineData(40, 'A', false)]
    [InlineData(41, 'a', false)]
    public void A_commit_sha_is_full_lower_case_hex(int length, char digit, bool valid) =>
        Assert.Equal(valid, CommitSha.TryParse(new string(digit, length), provider: null, out _));

    [Fact, Trait("Lane", "unit")]
    public void A_packet_hash_is_sha256_hex_and_exposes_its_digits()
    {
        var hex = new string('b', 64);

        Assert.Equal(hex, PacketHash.From("sha256:" + hex).Hex);
        Assert.False(PacketHash.TryParse("sha256:0", provider: null, out _));
        Assert.False(PacketHash.TryParse("md5:" + hex, provider: null, out _));
    }
}
