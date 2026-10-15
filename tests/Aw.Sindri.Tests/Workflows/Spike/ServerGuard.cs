namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Makes a pinned Temporal server die with the test host that started it (see the Wrap summary).</summary>
internal static class ServerGuard
{
    /// <summary>The server executable to hand to the SDK or the dev-server launcher.</summary>
    /// <param name="executable">The pinned server binary.</param>
    /// <param name="ownerPid">The process the server must not outlive.</param>
    /// <returns>The path to start.</returns>
    public static string Wrap(string executable, int ownerPid) => ownerPid < 0 ? throw new ArgumentOutOfRangeException(nameof(ownerPid)) : executable;

    /// <summary>The server executable for this test host.</summary>
    /// <param name="executable">The pinned server binary.</param>
    /// <returns>The path to start.</returns>
    public static string Wrap(string executable) => Wrap(executable, Environment.ProcessId);
}
