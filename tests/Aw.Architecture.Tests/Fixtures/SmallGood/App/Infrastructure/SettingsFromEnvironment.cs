namespace Aw.Architecture.Tests.Fixtures.SmallGood.App.Infrastructure;

internal static class SettingsFromEnvironment
{
    public static string StateDirectory(Func<string, string?> environment) =>
        environment("AW_STATE_DIR") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
