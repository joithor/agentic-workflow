namespace Aw.Architecture.Tests.TmuxFixtures;

/// <summary>Builds the attach line as data.</summary>
internal static class GoodAttachLine
{
    internal static string[] Build(string socket, string session) => ["tmux", "-S", socket, "attach", "-t", "=" + session];
}
