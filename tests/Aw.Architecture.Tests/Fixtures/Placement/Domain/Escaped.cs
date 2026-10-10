namespace Aw.Architecture.Tests.Fixtures.Placement.Domain;

internal static class Escaped
{
    public static bool Exists(string path) => File.Exists(path);
}
