using System.Diagnostics;
using System.Runtime.InteropServices;
using Aw.Packets.Testing;
using Aw.Packets.Tests.Contracts;

namespace Aw.Packets.Tests.Cli;

/// <summary>A temp git repository with a packet in it, a state root, and the built aw-packets to run against them.</summary>
internal sealed class CliWorkspace : IDisposable
{
    private readonly TempDirectory _root = new();

    /// <summary>Creates the repository (one commit of <see cref="PacketBuilder.BaseFiles"/>) and the state root.</summary>
    public CliWorkspace()
    {
        Repo = _root.Write("repo", PacketBuilder.BaseFiles.ToDictionary(static f => f, static f => "x\n"u8.ToArray(), StringComparer.Ordinal));
        _ = Git("init", "-q");
        _ = Git("add", "-A");
        _ = Git("-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "base");
        Sha = Git("rev-parse", "HEAD").Trim();
        State = Path.Combine(_root.Path, "state");
        _ = Directory.CreateDirectory(State);
    }

    /// <summary>The temp root.</summary>
    public string Root => _root.Path;

    /// <summary>The repository.</summary>
    public string Repo { get; }

    /// <summary>The base commit.</summary>
    public string Sha { get; }

    /// <summary>The state root (AW_STATE_DIR).</summary>
    public string State { get; }

    /// <summary>Writes a packet at repo/packets/&lt;name&gt; based on the repository's commit.</summary>
    /// <param name="packet">The packet (its base sha is replaced).</param>
    /// <param name="name">The directory name.</param>
    /// <returns>The packet directory.</returns>
    public string WritePacket(PacketBuilder packet, string name = "demo") =>
        _root.Write(Path.Combine("repo", "packets", name), PacketFiles.Of(packet.Base(Sha).Build()));

    /// <summary>Writes a scope packet at repo/packets/&lt;name&gt;.</summary>
    /// <param name="packet">The packet's files.</param>
    /// <param name="name">The directory name.</param>
    /// <returns>The packet directory.</returns>
    public string WriteScopingPacket(ScopingPacketBuilder packet, string name = "scope") =>
        _root.Write(Path.Combine("repo", "packets", name), packet.Build());

    /// <summary>Every file under the state root, relative to it.</summary>
    /// <returns>The paths, sorted.</returns>
    public IReadOnlyList<string> StateFiles() =>
        [.. Directory.EnumerateFiles(State, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(State, f)).Order(StringComparer.Ordinal)];

    /// <summary>Runs aw-packets with AW_STATE_DIR set to <see cref="State"/>.</summary>
    /// <param name="args">Its arguments.</param>
    /// <returns>The exit code, stdout and stderr.</returns>
    public (int Exit, string Out, string Err) Run(params string[] args)
    {
        var start = new ProcessStartInfo(DotnetHost())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Root,
        };
        foreach (var arg in (string[])["exec", Path.Combine(AppContext.BaseDirectory, "aw-packets.dll"), .. args])
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["AW_STATE_DIR"] = State;
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        // The tests compare stderr, so pin a level that writes no log lines (docs/csharp/telemetry.md, the CLI stderr contract).
        start.Environment["AW_LOG_LEVEL"] = "none";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Result, error);
    }

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    /// <summary>Runs git in the repository.</summary>
    /// <param name="args">Its arguments.</param>
    /// <returns>Its stdout.</returns>
    public string Git(params string[] args) => GitRepoSnapshotContractTests.Git(Repo, args);

    private static string DotnetHost()
    {
        // The runtime directory is <dotnet root>/shared/Microsoft.NETCore.App/<version>/.
        var root = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
        return Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
    }
}
