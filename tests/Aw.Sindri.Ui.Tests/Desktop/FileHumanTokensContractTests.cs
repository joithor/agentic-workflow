using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Integration)]
public sealed class FileHumanTokensContractTests : HumanTokenSourceContract, IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    protected override IHumanTokenSource With(string? text)
    {
        var path = Path.Combine(_dir, "home", "auth", "human.token");
        if (text is not null)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!); // why: Combine above always has a directory
            File.WriteAllText(path, text);
        }

        return new FileHumanTokens(path);
    }
}
