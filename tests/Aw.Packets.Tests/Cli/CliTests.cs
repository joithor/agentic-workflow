using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Cli;

/// <summary>The aw-packets CLI end to end: arguments, output, exit codes, and that it writes only under packets/.</summary>
[Lane(TestLane.Integration)]
public sealed class CliTests : IDisposable
{
    private readonly CliWorkspace _work = new();

    private Normalization Normal => new(Normalization.TempPath(_work.Root), new NormalizationRule("sha", "[0-9a-f]{12,64}", "<sha>"));

    public void Dispose() => _work.Dispose();

    [Fact]
    public void Validate_prints_every_rule_and_exits_0_for_a_sound_packet_and_writes_nothing()
    {
        var packet = _work.WritePacket(A.Packet());

        var (exit, output, error) = _work.Run("validate", packet, "--repo", _work.Repo);

        Assert.Equal((0, ""), (exit, error));
        Golden.Match(output, typeof(CliTests).Assembly, "Cli/Golden/validate-pass.txt", Normal);
        Assert.Empty(_work.StateFiles());
    }

    [Fact]
    public void Validate_json_is_one_versioned_document_with_one_record_per_failure()
    {
        var packet = _work.WritePacket(A.Packet().With(A.Task("t1").After("t2").Writing("src/a.cs")).With(A.Task("t2").After("t1").Writing("src/b.cs")));

        var (exit, output, _) = _work.Run("validate", packet, "--repo", _work.Repo, "--json");

        Assert.Equal(1, exit);
        using var report = JsonDocument.Parse(output);
        Assert.Equal(1, report.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(report.RootElement.GetProperty("passed").GetBoolean());
        var failures = report.RootElement.GetProperty("checks").EnumerateArray().Where(static c => c.GetProperty("state").GetString() == "fail").ToList();
        Assert.Equal(["PKT-GRAPH-002", "PKT-HOLD-001"], failures.Select(static c => c.GetProperty("rule").GetString()), StringComparer.Ordinal);
        Assert.All(failures, static f => Assert.False(string.IsNullOrEmpty(f.GetProperty("fix").GetString())));
    }

    [Fact]
    public void Validate_a_failing_packet_exits_1_and_names_the_rule_and_the_fix()
    {
        var packet = _work.WritePacket(A.Packet().With(A.Task("t1").After("t1").Example("h", holdout: true, "test")));

        var (exit, output, _) = _work.Run("validate", packet, "--repo", _work.Repo);

        Assert.Equal(1, exit);
        Golden.Match(output, typeof(CliTests).Assembly, "Cli/Golden/validate-fail.txt", Normal);
    }

    [Theory]
    [InlineData("missing", 2, "AW-PACKETS-002")]
    [InlineData("malformed", 1, "AW-PACKETS-003")]
    [InlineData("not-a-repo", 2, "AW-PACKETS-006")]
    public void Errors_go_to_stderr_with_their_code_and_exit_code(string kind, int expectedExit, string code)
    {
        var packet = _work.WritePacket(A.Packet());
        var repo = _work.Repo;
        switch (kind)
        {
            case "missing":
                packet = Path.Combine(_work.Root, "nowhere");
                break;
            case "malformed":
                File.WriteAllText(Path.Combine(packet, "tasks.json"), "{");
                break;
            default:
                repo = Path.Combine(_work.Root, "state");
                break;
        }

        var (exit, output, error) = _work.Run("validate", packet, "--repo", repo);

        Assert.Equal((expectedExit, ""), (exit, output));
        Assert.StartsWith($"aw-packets: {code} ", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("validate")]
    [InlineData("validate", "a", "b")]
    [InlineData("validate", "a", "--repo")]
    [InlineData("validate", "a", "--nope")]
    [InlineData("frobnicate", "a")]
    public void A_usage_error_exits_2(params string[] args) =>
        Assert.Equal(2, _work.Run(args).Exit);

    [Fact]
    public void Freeze_records_once_under_packets_and_never_touches_the_packet()
    {
        var packet = _work.WritePacket(A.Packet());
        _ = _work.Git("add", "-A");
        _ = _work.Git("-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "packet");

        var (firstExit, firstOut, _) = _work.Run("freeze", packet, "--repo", _work.Repo);
        var (secondExit, secondOut, _) = _work.Run("freeze", packet, "--repo", _work.Repo, "--json");

        Assert.Equal(0, firstExit);
        Assert.Matches("^aw-packets freeze: packet demo-packet — frozen sha256:[0-9a-f]{64} \\(packets/demo-packet/frozen/[0-9a-f]{64}\\.json\\)\n$", firstOut);
        Assert.Equal(0, secondExit);
        using var json = JsonDocument.Parse(secondOut);
        Assert.True(json.RootElement.GetProperty("alreadyFrozen").GetBoolean());
        var record = Assert.Single(_work.StateFiles());
        Assert.StartsWith(Path.Combine("packets", "demo-packet", "frozen") + Path.DirectorySeparatorChar, record, StringComparison.Ordinal);
        Assert.Equal("", _work.Git("status", "--porcelain"));
    }

    [Fact]
    public void Freeze_a_failing_packet_exits_1_and_writes_nothing()
    {
        var packet = _work.WritePacket(A.Packet().With(A.Task("t1").Writing("lib/**").Example("h", holdout: true, "test")));

        var (exit, output, _) = _work.Run("freeze", packet, "--repo", _work.Repo);

        Assert.Equal(1, exit);
        Assert.StartsWith("aw-packets freeze: packet demo-packet — not frozen: FAIL (1 failed, 3 unchecked)", output, StringComparison.Ordinal);
        Assert.Empty(_work.StateFiles());
    }
}
