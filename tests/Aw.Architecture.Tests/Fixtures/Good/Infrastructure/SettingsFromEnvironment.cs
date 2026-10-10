namespace Aw.Architecture.Tests.Fixtures.Good.Infrastructure;

internal static class SettingsFromEnvironment
{
    public static GoodSettings Read(Func<string, string?> environment) =>
        new(environment("AW_STATE_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow"));
}
