namespace Aw.Architecture.Tests.Fixtures.Good.Host.Commands;

internal static class CommandTable
{
    public static int Run(IServiceProvider services, string[] args) =>
        args.Length > 0 && args[0] == "start" ? StartItemCommand.Run(services, args.Length > 1 ? args[1] : string.Empty) : 2;
}
