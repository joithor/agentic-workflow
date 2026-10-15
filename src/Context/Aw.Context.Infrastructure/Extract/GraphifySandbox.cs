namespace Aw.Context.Infrastructure.Extract;

/// <summary>Wraps a command so it runs with the network denied and writes confined to given folders (spec §6.2); it fails closed: no sandbox, no command.</summary>
internal static class GraphifySandbox
{
    /// <summary>Wraps <paramref name="command"/>.</summary>
    /// <param name="host">The machine.</param>
    /// <param name="command">The command and its arguments.</param>
    /// <param name="writable">The only folders it may write.</param>
    /// <returns>The argument vector, or null when this machine has no sandbox.</returns>
    public static IReadOnlyList<string>? Argv(SandboxHost host, IReadOnlyList<string> command, IReadOnlyList<string> writable) =>
        throw Pending.Because(host, command, writable);
}
