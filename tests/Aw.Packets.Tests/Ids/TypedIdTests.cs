using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Tests.Ids;

/// <summary>Every Packets id and typed value round-trips through the Kernel profile; invalid, missing or null is AW-KERNEL-001 (R9).</summary>
public sealed class TypedIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    private static readonly string _sha = new('a', 40);
    private static readonly string _hash = "sha256:" + new string('b', 64);

    public static TheoryData<string, string> Kinds => new()
    {
        { "packet", "ok-1" }, { "task", "ok-1" }, { "workstream", "ok-1" }, { "question", "ok-1" }, { "example", "ok-1" },
        { "repo", "owner/name" }, { "sha", _sha }, { "hash", _hash },
    };

    private static Result<string> RoundTrip(string kind, string value) => kind switch
    {
        "packet" => Through(new PacketIdHolder(PacketId.From(value)), static h => h.Id.Value),
        "task" => Through(new TaskIdHolder(TaskId.From(value)), static h => h.Id.Value),
        "workstream" => Through(new WorkstreamIdHolder(WorkstreamId.From(value)), static h => h.Id.Value),
        "question" => Through(new QuestionIdHolder(QuestionId.From(value)), static h => h.Id.Value),
        "example" => Through(new ExampleIdHolder(ExampleId.From(value)), static h => h.Id.Value),
        "repo" => Through(new RepoSlugHolder(RepoSlug.From(value)), static h => h.Id.Value),
        "sha" => Through(new CommitShaHolder(CommitSha.From(value)), static h => h.Id.Value),
        _ => Through(new PacketHashHolder(PacketHash.From(value)), static h => h.Id.Value),
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
            "example" => AwJson.Deserialize<ExampleIdHolder>(bytes, _json).Map(static h => h.Id.Value),
            "repo" => AwJson.Deserialize<RepoSlugHolder>(bytes, _json).Map(static h => h.Id.Value),
            "sha" => AwJson.Deserialize<CommitShaHolder>(bytes, _json).Map(static h => h.Id.Value),
            _ => AwJson.Deserialize<PacketHashHolder>(bytes, _json).Map(static h => h.Id.Value),
        };
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public void Round_trips_through_the_Kernel_profile(string kind, string value) =>
        Assert.Equal(value, RoundTrip(kind, value).Value);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Kinds))]
    public void Blank_missing_or_null_is_AW_KERNEL_001(string kind, string value)
    {
        _ = value;
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

    [Fact, Trait("Lane", "unit")]
    public void A_missing_hash_stays_null_and_a_bad_one_is_AW_KERNEL_001()
    {
        var empty = AwJson.Deserialize<OptionalPacketHashHolder>("{\"id\":null}"u8, _json);
        var bad = AwJson.Deserialize<OptionalPacketHashHolder>("{\"id\":\"sha256:0\"}"u8, _json);

        Assert.Null(empty.Value.Id);
        Assert.Equal(KernelErrors.Invalid.Code, bad.Error.Code);
    }
}
