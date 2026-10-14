using Aw.Sindri.Core.Mcp;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IProviderFiles"/> honours, run against the fake and the file adapter (over a scratch home).</summary>
public abstract class ProviderFilesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>How each file's path is shown (relative to the home, never the absolute path).</summary>
    /// <param name="file">Which file.</param>
    /// <returns>The shown path.</returns>
    public static string Shown(ProviderFile file) => file switch
    {
        ProviderFile.Claude => "~/.claude.json",
        ProviderFile.Codex => "~/.codex/config.toml",
        ProviderFile.Cursor => "~/.cursor/mcp.json",
        _ => throw new ArgumentOutOfRangeException(nameof(file), file, "not a provider file"),
    };

    /// <summary>Files in a home: each given file with its text; the others missing.</summary>
    /// <param name="files">The files.</param>
    /// <returns>The reader over that home.</returns>
    protected abstract IProviderFiles Create(IReadOnlyDictionary<ProviderFile, string> files);

    /// <summary>A home where <paramref name="file"/> exists but can't be read.</summary>
    /// <param name="file">Which file.</param>
    /// <returns>The reader over that home.</returns>
    protected abstract IProviderFiles CreateUnreadable(ProviderFile file);

    /// <summary>An unreadable file has no text and a problem that says why, never the content.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unreadable_file_has_a_problem_and_no_text()
    {
        var read = await CreateUnreadable(ProviderFile.Codex).Read(ProviderFile.Codex, Token);

        Expect.True(read.Text is null && read.Problem is { Length: > 0 });
        Expect.Equal(Shown(ProviderFile.Codex), read.Path);
    }

    /// <summary>Each file reads back with its text and its path shown under '~'.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Each_file_reads_back_under_the_home()
    {
        var files = Create(new Dictionary<ProviderFile, string>
        {
            [ProviderFile.Claude] = "{\"mcpServers\":{}}",
            [ProviderFile.Codex] = "[mcp_servers.other]\n",
            [ProviderFile.Cursor] = "{}",
        });

        foreach (var file in Enum.GetValues<ProviderFile>())
        {
            var read = await files.Read(file, Token);
            Expect.Equal(Shown(file), read.Path);
            Expect.True(read.Text is not null && read.Problem is null);
        }
    }

    /// <summary>A missing file has no text and no problem.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_file_has_no_text_and_no_problem()
    {
        var read = await Create(new Dictionary<ProviderFile, string>()).Read(ProviderFile.Cursor, Token);

        Expect.Equal(new ProviderFileRead(ProviderFile.Cursor, Shown(ProviderFile.Cursor), Text: null, Problem: null), read);
    }
}
