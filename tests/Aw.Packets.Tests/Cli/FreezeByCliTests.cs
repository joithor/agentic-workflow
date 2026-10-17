using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Cli;

/// <summary>LN-PKT5 through the CLI: 'freeze --by owner|machine' for the build, line and scoping kinds.</summary>
[Lane(TestLane.Integration)]
public sealed class FreezeByCliTests : IDisposable
{
    private readonly CliWorkspace _work = new();

    public void Dispose() => _work.Dispose();

    private string StoredBy()
    {
        var record = Assert.Single(_work.StateFiles());
        using var stored = JsonDocument.Parse(File.ReadAllText(Path.Combine(_work.State, record)));
        return stored.RootElement.GetProperty("by").GetString()!;
    }

    [Fact]
    public void Freeze_is_by_the_machine_when_no_by_is_given()
    {
        var packet = _work.WritePacket(A.Packet());

        var (exit, _, _) = _work.Run("freeze", packet, "--repo", _work.Repo);

        Assert.Equal(0, exit);
        Assert.Equal("machine", StoredBy());
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("machine")]
    public void Freeze_by_stores_who_froze_it_and_json_says_so(string by)
    {
        var packet = _work.WritePacket(A.Packet());

        var (exit, output, _) = _work.Run("freeze", packet, "--by", by, "--repo", _work.Repo, "--json");

        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(by, json.RootElement.GetProperty("by").GetString());
        Assert.Equal(by, StoredBy());
    }

    [Fact]
    public void A_line_packet_freeze_takes_by()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Phase));

        var (exit, _, _) = _work.Run("freeze", "--kind", "phase", "--by", "owner", packet, "--repo", _work.Repo);

        Assert.Equal(0, exit);
        Assert.Equal("owner", StoredBy());
    }

    [Fact]
    public void A_scoping_freeze_takes_by()
    {
        var packet = _work.WriteScopingPacket(A.ScopingPacket());

        var (exit, _, _) = _work.Run("freeze", "--kind", "scoping", packet, "--repo-slug", "example/repo", "--instance", "idea-1", "--by", "owner");

        Assert.Equal(0, exit);
        Assert.Equal("owner", StoredBy());
    }

    [Fact]
    public void An_owner_freeze_of_a_machine_frozen_hash_exits_0_says_it_was_not_recorded_and_keeps_machine()
    {
        var packet = _work.WritePacket(A.Packet());
        _ = _work.Run("freeze", packet, "--repo", _work.Repo);

        var (exit, output, _) = _work.Run("freeze", packet, "--by", "owner", "--repo", _work.Repo);
        var (jsonExit, jsonOutput, _) = _work.Run("freeze", packet, "--by", "owner", "--repo", _work.Repo, "--json");

        Assert.Equal((0, 0), (exit, jsonExit));
        Assert.Contains("already frozen sha256:", output, StringComparison.Ordinal);
        Assert.Contains("note: alreadyFrozenBy machine", output, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(jsonOutput);
        Assert.Equal(("machine", "machine", true), (
            json.RootElement.GetProperty("by").GetString(),
            json.RootElement.GetProperty("alreadyFrozenBy").GetString(),
            json.RootElement.GetProperty("alreadyFrozen").GetBoolean()));
        Assert.Equal("machine", StoredBy());
    }

    [Fact]
    public void A_repeat_freeze_by_the_same_who_has_no_note()
    {
        var packet = _work.WritePacket(A.Packet());
        _ = _work.Run("freeze", packet, "--by", "owner", "--repo", _work.Repo);

        var (_, output, _) = _work.Run("freeze", packet, "--by", "owner", "--repo", _work.Repo);

        Assert.DoesNotContain("alreadyFrozenBy", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("freeze", "x", "--by")]
    [InlineData("freeze", "x", "--by", "robot")]
    [InlineData("freeze", "x", "--by", "owner", "--by", "machine")]
    [InlineData("validate", "x", "--by", "owner")]
    [InlineData("hash", "x", "--by", "owner")]
    public void A_bad_or_misplaced_by_is_a_usage_error(params string[] args)
    {
        var (exit, _, error) = _work.Run(args);

        Assert.Equal(2, exit);
        Assert.StartsWith("usage: aw-packets ", error, StringComparison.Ordinal);
    }
}
