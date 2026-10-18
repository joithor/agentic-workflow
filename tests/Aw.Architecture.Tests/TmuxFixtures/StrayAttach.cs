namespace Aw.Architecture.Tests.TmuxFixtures;

/// <summary>Builds an attach argv outside the attach line type.</summary>
internal static class StrayAttach
{
    internal static string[] Build(string session) => ["attach", "-t", session];
}
