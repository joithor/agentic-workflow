using Aw.Context.Infrastructure.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class GraphifySandboxTests
{
    private static SandboxHost Host(bool linux = true, bool mac = false, bool rootBwrap = true) => new(linux, mac, "/home/avery", "/run/user/1000",
        path => rootBwrap && (string.Equals(path, "/usr/bin/bwrap", StringComparison.Ordinal) || string.Equals(path, "/usr/bin/sandbox-exec", StringComparison.Ordinal)),
        path => path switch { "/home/avery/.ssh" or "/run/user/1000" => PathKind.Directory, "/home/avery/.netrc" => PathKind.File, _ => PathKind.Missing },
        path => path);

    [Fact]
    public void On_linux_bwrap_denies_the_network_binds_only_the_snapshot_and_hides_credentials()
    {
        var argv = GraphifySandbox.Argv(Host(), ["graphify", "extract", "/snap"], ["/snap"])!;

        Assert.Equal("/usr/bin/bwrap", argv[0]);
        Assert.Contains("--unshare-all", argv, StringComparer.Ordinal);
        Assert.Contains("--die-with-parent", argv, StringComparer.Ordinal);
        Assert.True(argv.Skip(argv.ToList().IndexOf("--bind")).Take(3).SequenceEqual(["--bind", "/snap", "/snap"], StringComparer.Ordinal));
        Assert.Equal(1, argv.Count(a => a == "--bind"));
        Assert.Contains("/home/avery/.ssh", argv, StringComparer.Ordinal);      // an empty tmpfs over it
        Assert.Contains("/home/avery/.netrc", argv, StringComparer.Ordinal);    // /dev/null over it
        Assert.True(argv.TakeLast(3).SequenceEqual(["graphify", "extract", "/snap"], StringComparer.Ordinal));
    }

    [Fact]
    public void A_bwrap_that_is_not_root_owned_is_not_used() =>
        Assert.Null(GraphifySandbox.Argv(Host(rootBwrap: false), ["graphify"], ["/snap"]));

    [Fact]
    public void On_macos_sandbox_exec_denies_the_network_and_writes_outside_the_snapshot()
    {
        var argv = GraphifySandbox.Argv(Host(linux: false, mac: true), ["graphify", "extract", "/snap"], ["/snap"])!;

        Assert.True(argv.Take(2).SequenceEqual(["/usr/bin/sandbox-exec", "-p"], StringComparer.Ordinal));
        Assert.Contains("(deny network*)", argv[2], StringComparison.Ordinal);
        Assert.Contains("(subpath \"/snap\")", argv[2], StringComparison.Ordinal);
        Assert.True(argv.TakeLast(3).SequenceEqual(["graphify", "extract", "/snap"], StringComparer.Ordinal));
    }

    [Fact]
    public void Another_system_has_no_sandbox_and_so_no_command() =>
        Assert.Null(GraphifySandbox.Argv(Host(linux: false), ["graphify"], ["/snap"]));

    [Fact]
    public void The_pin_is_the_one_in_graphify_pin()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AgenticWorkflow.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("repo root not found");
        }

        var row = File.ReadAllLines(Path.Combine(root, "context", "graphify.pin"))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Single(fields => fields.Length == 3 && fields[0] == "graphify");

        Assert.Equal(row[1], GraphifyOptions.Default.Pin);
    }
}
