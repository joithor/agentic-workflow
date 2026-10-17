using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Cli;

/// <summary>The line kinds through the CLI: --kind, validate against a real repository, hash and freeze, and that only freeze writes (under packets/).</summary>
[Lane(TestLane.Integration)]
public sealed class LineCliTests : IDisposable
{
    private readonly CliWorkspace _work = new();

    public static TheoryData<string> Kinds => ["design", "plan", "phase", "proof", "review", "handoff"];

    public void Dispose() => _work.Dispose();

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Validate_passes_the_gold_packet_checks_the_base_commit_and_writes_nothing(string kind)
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Find(kind)!));

        var (exit, output, error) = _work.Run("validate", "--kind", kind, packet, "--repo", _work.Repo);

        Assert.Equal((0, ""), (exit, error));
        Assert.StartsWith($"aw-packets validate: {kind} packet ", output, StringComparison.Ordinal);
        Assert.Contains("— PASS (0 failed,", output, StringComparison.Ordinal);
        Assert.Contains("pass      PKT-BASE-001 packet: base commit ", output, StringComparison.Ordinal);
        Assert.Empty(_work.StateFiles());
    }

    [Fact]
    public void Validate_a_broken_packet_exits_1_and_json_has_one_record_per_failure_and_the_kind()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Phase).Without("design.md").Edit("holdout.json", "\"count\": 3", "\"count\": -1"));

        var (exit, output, _) = _work.Run("validate", packet, "--json", "--kind", "phase", "--repo", _work.Repo);

        Assert.Equal(1, exit);
        using var report = JsonDocument.Parse(output);
        Assert.Equal("phase", report.RootElement.GetProperty("kind").GetString());
        Assert.Equal("expiry-p1", report.RootElement.GetProperty("packet").GetString());
        Assert.False(report.RootElement.GetProperty("passed").GetBoolean());
        var failed = report.RootElement.GetProperty("checks").EnumerateArray()
            .Where(static c => c.GetProperty("state").GetString() == "fail").Select(static c => c.GetProperty("rule").GetString()).ToList();
        Assert.Equal(["PKT-PHS-001", "PKT-PHS-012"], failed);
    }

    [Fact]
    public void Hash_prints_the_hash_alone_and_agrees_with_freeze_and_json_lists_the_proof_files()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Proof));
        var (exit, hash, _) = _work.Run("hash", "--kind", "proof", packet);
        var (_, jsonOut, _) = _work.Run("hash", "--kind", "proof", packet, "--json");
        var (freezeExit, frozen, _) = _work.Run("freeze", "--kind", "proof", packet, "--repo", _work.Repo);

        Assert.Equal((0, 0), (exit, freezeExit));
        Assert.Matches("^sha256:[0-9a-f]{64}\n$", hash);
        using var json = JsonDocument.Parse(jsonOut);
        Assert.Equal("proof", json.RootElement.GetProperty("kind").GetString());
        Assert.Contains("evidence/shot.txt", json.RootElement.GetProperty("files").EnumerateArray().Select(static f => f.GetString()));
        Assert.Contains(hash.Trim(), frozen, StringComparison.Ordinal);
        Assert.Equal(hash.Trim(), json.RootElement.GetProperty("hash").GetString());
    }

    [Fact]
    public void Freeze_records_the_hash_under_the_packets_id_once_with_its_kind_and_base_sha()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Design));
        var hash = _work.Run("hash", "--kind", "design", packet).Out.Trim();

        var (firstExit, firstOut, _) = _work.Run("freeze", "--kind", "design", packet, "--repo", _work.Repo);
        var (secondExit, secondOut, _) = _work.Run("freeze", "--kind", "design", packet, "--repo", _work.Repo, "--json");

        Assert.Equal(0, firstExit);
        Assert.Equal($"aw-packets freeze: design packet expiry-design — frozen {hash} (packets/example/repo/expiry-design/frozen/{hash["sha256:".Length..]}.json)\n", firstOut);
        Assert.Equal(0, secondExit);
        using var json = JsonDocument.Parse(secondOut);
        Assert.True(json.RootElement.GetProperty("alreadyFrozen").GetBoolean());
        var record = Assert.Single(_work.StateFiles());
        using var stored = JsonDocument.Parse(File.ReadAllText(Path.Combine(_work.State, record)));
        Assert.Equal("design", stored.RootElement.GetProperty("kind").GetString());
        Assert.Equal(hash, stored.RootElement.GetProperty("hash").GetString());
        Assert.Equal(_work.Sha, stored.RootElement.GetProperty("baseSha").GetString());
    }

    [Fact]
    public void Freeze_a_failing_packet_exits_1_and_writes_nothing()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Review).With("extra.txt", "x"));

        var (exit, output, _) = _work.Run("freeze", "--kind", "review", packet, "--repo", _work.Repo);

        Assert.Equal(1, exit);
        Assert.StartsWith("aw-packets freeze: review packet expiry-review — not frozen: FAIL (1 failed, 0 unchecked)", output, StringComparison.Ordinal);
        Assert.Empty(_work.StateFiles());
    }

    [Fact]
    public void A_base_sha_the_repository_does_not_have_fails_PKT_BASE_001_for_a_line_kind()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Handoff));
        var text = File.ReadAllText(Path.Combine(packet, "packet.json")).Replace(_work.Sha, "0000000000000000000000000000000000000001", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(packet, "packet.json"), text);

        var (exit, output, _) = _work.Run("validate", "--kind", "handoff", packet, "--repo", _work.Repo);

        Assert.Equal(1, exit);
        Assert.Contains("FAIL      PKT-BASE-001 packet: baseSha 0000000000000000000000000000000000000001 isn't in the repository", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("validate", "--kind", "design")]
    [InlineData("validate", "--kind", "plan", "x", "--repo-slug", "y")]
    [InlineData("freeze", "--kind", "phase", "x", "--instance", "y")]
    [InlineData("hash", "--kind", "proof")]
    public void A_usage_error_exits_2(params string[] args) =>
        Assert.Equal(2, _work.Run(args).Exit);
}
