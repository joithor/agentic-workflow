using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Xunit;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The contract every <see cref="IBriefFiles"/> honours, run against the in-memory reader and the disk one (scoping spec §8.2, SND-SCOPING-004).</summary>
public abstract class BriefFilesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A reader over a fresh root that holds <paramref name="files"/> (text by path relative to the root).</summary>
    /// <param name="files">The files.</param>
    /// <param name="root">The root the reader is to be given.</param>
    /// <returns>The reader.</returns>
    protected abstract IBriefFiles Create(IReadOnlyDictionary<string, string> files, out string root);

    /// <summary>A file under the root reads back with its path, its text and a version that is the text's hash.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_file_under_the_root_reads_back_with_the_hash_of_its_text()
    {
        var reader = Create(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes/brief.md"] = "# Brief\n\nText." }, out var root);

        var read = await reader.Read("notes/brief.md", [root], Token);

        Expect.True(read.IsSuccess);
        Expect.Equal("notes/brief.md", read.Value.Path);
        Expect.Equal("# Brief\n\nText.", read.Value.Text);
        Expect.True(System.Text.RegularExpressions.Regex.IsMatch(read.Value.Version, "^sha256:[0-9a-f]{12}$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    /// <summary>A file that is not there is invalid input, not a refusal.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_file_is_invalid_input()
    {
        var reader = Create(new Dictionary<string, string>(StringComparer.Ordinal), out var root);

        var read = await reader.Read("absent.md", [root], Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, read.Error.Code);
    }

    /// <summary>With no root there is nothing to read from.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_root_is_invalid_input()
    {
        var reader = Create(new Dictionary<string, string>(StringComparer.Ordinal) { ["a.md"] = "x" }, out _);

        var read = await reader.Read("a.md", [], Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, read.Error.Code);
    }

    /// <summary>A path that climbs out of the root is refused with SND-SCOPING-004.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_path_outside_the_root_is_refused()
    {
        var reader = Create(new Dictionary<string, string>(StringComparer.Ordinal) { ["a.md"] = "x" }, out var root);

        var read = await reader.Read("../a.md", [root], Token);

        Expect.Equal(ScopingErrors.BriefPathRefused.Code, read.Error.Code);
    }

    /// <summary>A file under a denied glob is refused even when it exists.</summary>
    /// <param name="path">The denied path.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData(".env")]
    [InlineData("config/.env.local")]
    [InlineData("keys/server.pem")]
    [InlineData(".git/config")]
    [InlineData("deploy/credentials.json")]
    public async Task A_denied_file_is_refused_even_when_it_exists(string path)
    {
        var reader = Create(new Dictionary<string, string>(StringComparer.Ordinal) { [path] = "secret" }, out var root);

        var read = await reader.Read(path, [root], Token);

        Expect.Equal(ScopingErrors.BriefPathRefused.Code, read.Error.Code);
    }

    /// <summary>A brief over 1 MiB is invalid input that says to name a section or a smaller file.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_file_over_the_limit_is_invalid_input()
    {
        var reader = Create(new Dictionary<string, string>(StringComparer.Ordinal) { ["big.md"] = new string('a', (int)BriefPaths.MaxBytes + 1) }, out var root);

        var read = await reader.Read("big.md", [root], Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, read.Error.Code);
    }
}
