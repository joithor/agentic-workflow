using Aw.Architecture.Tests.Fixtures.SmallGood.App.Features.RunJob;

namespace Aw.Architecture.Tests.Fixtures.SmallGood.App.Commands;

internal static class RunJobCommand
{
    public static int Run(IServiceProvider services, string name) =>
        services.GetService(typeof(RunJobService)) is null ? 1 : RunJobService.Run(name).Name.Length;
}
