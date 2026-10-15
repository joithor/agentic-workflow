using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Integration)]
public sealed class FileHumanTokenContractTests : HumanTokenSourceContract, IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string TokenPath => Path.Combine(_dir, "home", "auth", "human.token");

    protected override IHumanTokenSource With(string? contents)
    {
        Write(contents);
        return new FileHumanToken(TokenPath);
    }

    protected override void Rewrite(IHumanTokenSource source, string? contents) => Write(contents);

    private void Write(string? contents)
    {
        if (contents is null)
        {
            if (File.Exists(TokenPath))
            {
                File.Delete(TokenPath);
            }

            return;
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(TokenPath)!); // why: Combine above always has a directory
        File.WriteAllText(TokenPath, contents);
    }

    // A token file the platform can't read (here a directory in its place) throws, like the endpoint record: the
    // client then sends no token and Sindri's refusal says why (never the platform's text in the window).
    [Fact]
    public async Task A_token_the_platform_cant_read_throws_an_io_error()
    {
        _ = Directory.CreateDirectory(TokenPath);

        var error = await Record.ExceptionAsync(async () => await new FileHumanToken(TokenPath).Read(TestContext.Current.CancellationToken));

        Assert.True(error is IOException or UnauthorizedAccessException, error?.GetType().Name ?? "no exception");
    }
}
