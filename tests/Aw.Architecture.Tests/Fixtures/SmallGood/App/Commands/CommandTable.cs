namespace Aw.Architecture.Tests.Fixtures.SmallGood.App.Commands;

internal static class CommandTable
{
    public static int Run(IServiceProvider services, string name) => RunJobCommand.Run(services, name);
}
