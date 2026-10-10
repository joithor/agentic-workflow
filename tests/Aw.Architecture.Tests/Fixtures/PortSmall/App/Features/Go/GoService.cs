namespace Aw.Architecture.Tests.Fixtures.PortSmall.App.Features.Go;

internal static class GoService
{
    public static string? Home => Environment.GetEnvironmentVariable("HOME");
}
