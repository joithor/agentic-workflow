namespace Aw.Architecture.Tests.Fixtures.PortSmall.App.Commands;

internal static class ReadCommand
{
    public static string Run(string path) => File.ReadAllText(path);
}
