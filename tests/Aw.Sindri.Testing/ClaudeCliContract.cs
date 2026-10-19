using Aw.Sindri.Core.Mcp;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IClaudeCli"/> honours, run against the fake and the process adapter (over a recording stub, never the real claude).</summary>
public abstract class ClaudeCliContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A CLI whose runs exit with <paramref name="exitCode"/> after writing <paramref name="error"/> to stderr; null for one that can't be started.</summary>
    /// <param name="exitCode">The exit code, or null for a missing executable.</param>
    /// <param name="error">Its stderr.</param>
    /// <returns>The CLI.</returns>
    protected abstract IClaudeCli Create(int? exitCode, string error);

    /// <summary>The runs the CLI received: arguments and working directory.</summary>
    /// <returns>The runs.</returns>
    protected abstract IReadOnlyList<(IReadOnlyList<string> Arguments, string Directory)> Received();

    /// <summary>A directory to run in.</summary>
    /// <returns>The directory.</returns>
    protected abstract string Directory();

    /// <summary>The arguments arrive one by one, unsplit, in the given directory.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Arguments_arrive_unsplit_in_the_directory()
    {
        string[] arguments = ["mcp", "add", "sindri", "--header", "X-Sindri-Repo: demo repo"];
        var run = await Create(0, string.Empty).Run(arguments, Directory(), Token);

        Expect.Equal(0, run!.ExitCode);   // why: the CLI started
        var received = Expect.Only(Received());
        Expect.Sequence(arguments, received.Arguments);
        Expect.Equal(Directory(), received.Directory);
    }

    /// <summary>A failure reports its exit code and the last line of stderr.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failure_reports_its_code_and_last_error_line()
    {
        var run = await Create(3, "first\nMCP server sindri already exists\n").Run(["mcp"], Directory(), Token);

        Expect.Equal(new ClaudeRun(3, "MCP server sindri already exists"), run);
    }

    /// <summary>A directory that doesn't exist (the CLI under test must know it as missing).</summary>
    /// <returns>The path.</returns>
    protected abstract string MissingDirectory();

    /// <summary>A run in a directory that doesn't exist is null, and nothing runs.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_directory_is_null_and_nothing_runs()
    {
        var cli = Create(0, string.Empty);

        Expect.Equal(expected: null, await cli.Run(["mcp"], MissingDirectory(), Token));
        Expect.Equal(0, Received().Count);
    }

    /// <summary>An executable that can't be started is null, not an exception.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_executable_is_null() =>
        Expect.Equal(expected: null, await Create(exitCode: null, string.Empty).Run(["mcp"], Directory(), Token));
}
