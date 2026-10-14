using Aw.Kernel.Testing;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Infrastructure.Mcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The file adapter over a scratch home (never the real one).</summary>
[Lane(TestLane.Integration)]
public sealed class ProviderConfigFilesContractTests : ProviderFilesContract, IDisposable
{
    private readonly DirectoryInfo _home = Directory.CreateTempSubdirectory("provider-home-");

    public void Dispose() => _home.Delete(recursive: true);

    [Fact]
    public async Task CLAUDE_CONFIG_DIR_and_CODEX_HOME_move_those_files()
    {
        var claude = Directory.CreateDirectory(Path.Combine(_home.FullName, "claude-config")).FullName;
        var codex = Directory.CreateDirectory(Path.Combine(_home.FullName, "codex-home")).FullName;
        await File.WriteAllTextAsync(Path.Combine(claude, ".claude.json"), "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(codex, "config.toml"), "", TestContext.Current.CancellationToken);
        var files = new ProviderConfigFiles(McpSettings.Read(name => name switch
        {
            "HOME" => _home.FullName,
            "CLAUDE_CONFIG_DIR" => claude,
            "CODEX_HOME" => codex,
            _ => null,
        }));

        var claudeRead = await files.Read(ProviderFile.Claude, TestContext.Current.CancellationToken);
        var codexRead = await files.Read(ProviderFile.Codex, TestContext.Current.CancellationToken);

        Assert.Equal(("~/claude-config/.claude.json", "{}"), (claudeRead.Path, claudeRead.Text));
        Assert.Equal(("~/codex-home/config.toml", ""), (codexRead.Path, codexRead.Text));
    }

    protected override IProviderFiles Create(IReadOnlyDictionary<ProviderFile, string> files)
    {
        foreach (var (file, text) in files)
        {
            var path = Path.Combine(_home.FullName, Shown(file)[2..]);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: every shown path has a directory
            File.WriteAllText(path, text);
        }

        return new ProviderConfigFiles(McpSettings.Read(name => name == "HOME" ? _home.FullName : null));
    }

    protected override IProviderFiles CreateUnreadable(ProviderFile file)
    {
        var files = Create(new Dictionary<ProviderFile, string> { [file] = "[mcp_servers.sindri]\n" });
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path.Combine(_home.FullName, Shown(file)[2..]), UnixFileMode.None);
        }

        return files;
    }
}
