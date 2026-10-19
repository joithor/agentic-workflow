using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Packets;

/// <summary>
/// The aw-packets adapter against the real built aw-packets (src/Packets/Aw.Packets.App, built by the solution build): the JSON it reads,
/// on the line fixture's gold design packet. Freeze writes under a temp AW_STATE_DIR set by the wrapper script.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class AwPacketsToolRealBinaryTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private AwPacketsTool Tool()
    {
        var wrapper = Path.Combine(_home.Path, "aw-packets");
        File.WriteAllText(wrapper, $"#!/bin/sh\nAW_STATE_DIR='{Path.Combine(_home.Path, "state")}'\nexport AW_STATE_DIR\nexec dotnet '{BuiltDll()}' \"$@\"\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return new AwPacketsTool(new ProcessToolRunner(), name => string.Equals(name, "AW_PACKETS_BIN", StringComparison.Ordinal) ? wrapper : null);
    }

    private PacketRef Packet(string name)
    {
        var target = Path.Combine(_home.Path, name);
        CopyDirectory(Path.Combine(RepoRoot(), "tests", "fixtures", "line", "gold", "design", "expiry"), target);
        return new PacketRef(PacketKind.Design, target, Repo: FixtureRepo());
    }

    // The packet's baseSha is a commit of the scoping fixture's history; the script builds it deterministically.
    private string FixtureRepo()
    {
        var parent = Path.Combine(_home.Path, "history");
        var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(RepoRoot(), "tests", "fixtures", "scoping", "history", "build.sh"));
        start.ArgumentList.Add(parent);
        using var process = Process.Start(start)!;   // why: the program is bash, which starts
        var output = process.StandardOutput.ReadToEndAsync(Token);
        var error = process.StandardError.ReadToEndAsync(Token);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output.Result + error.Result);
        return Path.Combine(parent, "fixture-notes");
    }

    [Fact]
    public async Task Validate_hash_and_freeze_read_what_the_real_aw_packets_prints()
    {
        var tool = Tool();
        var packet = Packet("good");

        var validation = await tool.Validate(packet, Token);
        var hash = await tool.Hash(packet, Token);
        var again = await tool.Hash(packet, Token);
        var first = await tool.Freeze(packet, PacketFreezer.Machine, Token);
        var second = await tool.Freeze(packet, PacketFreezer.Machine, Token);

        Assert.True(validation.Value.Passed, string.Join("; ", validation.Value.Failures));
        Assert.Matches("^sha256:[0-9a-f]{64}$", hash.Value);
        Assert.Equal(hash.Value, again.Value);
        Assert.True(first.Value.Frozen);
        Assert.False(first.Value.AlreadyFrozen);
        Assert.Equal(hash.Value, first.Value.Hash);
        Assert.Equal(PacketFreezer.Machine, first.Value.By);
        Assert.True(second.Value.Frozen);
        Assert.True(second.Value.AlreadyFrozen);
        Assert.Equal(hash.Value, second.Value.Hash);
    }

    [Fact]
    public async Task A_broken_packet_fails_validate_with_its_rule_and_freezes_nothing()
    {
        var tool = Tool();
        var packet = Packet("broken");
        File.Delete(Path.Combine(packet.Directory, "signals.json"));

        var validation = await tool.Validate(packet, Token);
        var freeze = await tool.Freeze(packet, PacketFreezer.Machine, Token);

        Assert.False(validation.Value.Passed);
        Assert.NotEmpty(validation.Value.Failures);
        Assert.All(validation.Value.Failures, failure => Assert.StartsWith("PKT-", failure.Rule, StringComparison.Ordinal));
        Assert.False(freeze.Value.Frozen);
        Assert.NotEmpty(freeze.Value.Failures);
        Assert.False(Directory.Exists(Path.Combine(_home.Path, "state", "packets")));
    }

    private static void CopyDirectory(string from, string to)
    {
        _ = Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
    }

    private static string BuiltDll()
    {
        var bin = Path.Combine(RepoRoot(), "artifacts", "bin", "Aw.Packets.App");
        var dlls = Directory.Exists(bin) ? Directory.GetFiles(bin, "aw-packets.dll", SearchOption.AllDirectories) : [];
        Assert.True(dlls.Length > 0, "aw-packets is not built: dotnet build src/Packets/Aw.Packets.App");
        return dlls.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }
}
