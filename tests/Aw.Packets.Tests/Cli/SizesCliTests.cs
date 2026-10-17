using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Model;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Cli;

/// <summary>LN-PKT3 through the CLI: 'validate --sizes &lt;file&gt;'.</summary>
[Lane(TestLane.Integration)]
public sealed class SizesCliTests : IDisposable
{
    private readonly CliWorkspace _work = new();

    public void Dispose() => _work.Dispose();

    private string WriteSizes(SizesFile sizes, string name = "sizes.json")
    {
        var path = Path.Combine(_work.Root, name);
        File.WriteAllBytes(path, SizesBuilder.Bytes(sizes));
        return path;
    }

    [Fact]
    public void Validate_without_sizes_leaves_PKT_SIZE_001_unchecked()
    {
        var packet = _work.WritePacket(A.Packet());

        var (exit, output, _) = _work.Run("validate", packet, "--repo", _work.Repo);

        Assert.Equal(0, exit);
        Assert.Contains("unchecked PKT-SIZE-001 packet: no sizes file was given", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_with_sizes_passes_a_task_within_its_size()
    {
        var packet = _work.WritePacket(A.Packet());

        var (exit, output, _) = _work.Run("validate", packet, "--repo", _work.Repo, "--sizes", WriteSizes(A.Sizes()));

        Assert.Equal(0, exit);
        Assert.Contains("pass      PKT-SIZE-001 packet: every task fits its size class", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_with_sizes_fails_an_oversized_task_exits_1_and_json_names_the_task()
    {
        var packet = _work.WritePacket(A.Packet().With(A.Task("t1").Sized(SizeClass.ExtraSmall).Example("e1", holdout: false, "test").Example("e2", holdout: false, "test").Example("h1", holdout: true, "test")));

        var (exit, output, _) = _work.Run("validate", packet, "--repo", _work.Repo, "--sizes", WriteSizes(A.Sizes()), "--json");

        Assert.Equal(1, exit);
        using var report = JsonDocument.Parse(output);
        var failure = Assert.Single(report.RootElement.GetProperty("checks").EnumerateArray(), static c => c.GetProperty("state").GetString() == "fail");
        Assert.Equal(("PKT-SIZE-001", "task t1"), (failure.GetProperty("rule").GetString(), failure.GetProperty("subject").GetString()));
    }

    [Fact]
    public void A_plan_graph_takes_sizes()
    {
        var packet = _work.WriteLinePacket(A.LinePacket(PacketKinds.Plan));

        var (exit, output, _) = _work.Run("validate", "--kind", "plan", packet, "--repo", _work.Repo, "--sizes", WriteSizes(A.Sizes()));

        Assert.Equal(0, exit);
        Assert.Contains("pass      PKT-SIZE-001", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_or_bad_sizes_file_is_AW_PACKETS_009_and_exit_2()
    {
        var packet = _work.WritePacket(A.Packet());
        File.WriteAllText(Path.Combine(_work.Root, "bad.json"), "{}");

        var (missingExit, _, missingError) = _work.Run("validate", packet, "--repo", _work.Repo, "--sizes", Path.Combine(_work.Root, "nowhere.json"));
        var (badExit, _, badError) = _work.Run("validate", packet, "--repo", _work.Repo, "--sizes", Path.Combine(_work.Root, "bad.json"));

        Assert.Equal((2, 2), (missingExit, badExit));
        Assert.StartsWith("aw-packets: AW-PACKETS-009 ", missingError, StringComparison.Ordinal);
        Assert.StartsWith("aw-packets: AW-PACKETS-009 ", badError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("validate", "x", "--sizes")]
    [InlineData("validate", "x", "--kind", "design", "--sizes", "s.json")]
    [InlineData("validate", "x", "--kind", "scoping", "--sizes", "s.json")]
    [InlineData("freeze", "x", "--sizes", "s.json")]
    [InlineData("hash", "x", "--sizes", "s.json")]
    public void Sizes_is_for_validate_of_a_build_or_plan_packet_only(params string[] args)
    {
        var (exit, _, error) = _work.Run(args);

        Assert.Equal(2, exit);
        Assert.StartsWith("usage: aw-packets ", error, StringComparison.Ordinal);
    }
}
