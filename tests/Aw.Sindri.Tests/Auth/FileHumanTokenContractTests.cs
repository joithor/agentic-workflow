using System.Buffers.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Auth;

[Lane(TestLane.Integration)]
public sealed class FileHumanTokenContractTests : HumanTokenStoreContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SindriSettings Settings => new(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"));

    protected override IHumanTokenStore Create() => new FileHumanToken(Settings);

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Minted_token_file_is_0600_in_a_0700_folder()
    {
        var token = (await Create().Mint(Token)).Value;

        Assert.Equal(token.Value, (await File.ReadAllTextAsync(Settings.TokenPath, Token)).Trim());
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Settings.TokenPath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.GetDirectoryName(Settings.TokenPath)!));   // why: TokenPath is a file under auth/
    }

    [Fact]
    public async Task A_minted_token_is_32_random_bytes_in_base64url()
    {
        var token = (await Create().Mint(Token)).Value;

        Assert.Equal(32, Base64Url.DecodeFromChars(token.Value).Length);
        Assert.DoesNotContain('=', token.Value);
    }

    [Fact]
    public async Task An_empty_token_file_reads_as_no_token()
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(Settings.TokenPath)!);   // why: TokenPath is a file under auth/
        await File.WriteAllTextAsync(Settings.TokenPath, "\n", Token);

        Assert.Null((await Create().Read(Token)).Value);
    }
}
