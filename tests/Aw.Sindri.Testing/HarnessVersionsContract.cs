using Aw.Sindri.Core.Control;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IHarnessVersions"/> honours (engine spec §6.2), run against the in-memory table and the
/// '&lt;command&gt; --version' reader: the first version number a working command prints, else null; never an error.
/// </summary>
public abstract class HarnessVersionsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The versions under test.</summary>
    /// <returns>The reader.</returns>
    protected abstract IHarnessVersions Create();

    /// <summary>A command that prints '<paramref name="output"/>' and exits 0.</summary>
    /// <param name="output">What '--version' prints.</param>
    /// <param name="version">The version a reader should find in it.</param>
    /// <returns>The command.</returns>
    protected abstract string Installed(string output, string version);

    /// <summary>A command that prints a version but exits 1.</summary>
    /// <returns>The command.</returns>
    protected abstract string Failing();

    /// <summary>A command that isn't installed.</summary>
    /// <returns>The command.</returns>
    protected abstract string Missing();

    /// <summary>A command that writes far more than a pipe buffer to stderr, then prints '<paramref name="version"/>' and exits 0.</summary>
    /// <param name="version">The version it prints.</param>
    /// <returns>The command.</returns>
    protected abstract string Noisy(string version);

    /// <summary>The first version number of the output is the version.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_installed_command_reads_its_first_version_number()
    {
        var versions = Create();

        Expect.Equal("2.1.296", await versions.Read(Installed("2.1.296 (Claude Code)", "2.1.296"), Token));
        Expect.Equal("0.41.0", await versions.Read(Installed("codex-cli 0.41.0", "0.41.0"), Token));
    }

    /// <summary>A missing or failing command, or one that prints no version, has no version.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_failing_or_versionless_command_has_none()
    {
        var versions = Create();

        Expect.True(await versions.Read(Missing(), Token) is null);
        Expect.True(await versions.Read(Failing(), Token) is null);
        Expect.True(await versions.Read(Installed("no version here", version: string.Empty), Token) is null);
    }

    /// <summary>Output on stderr, however much, doesn't hide the version.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Stderr_output_does_not_hide_the_version() =>
        Expect.Equal("1.2.3", await Create().Read(Noisy("1.2.3"), Token));
}
