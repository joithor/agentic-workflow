using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri services …' and 'sindri jobs …' (API P6 unit H): the service and job verbs, with the job follower they share.</summary>
internal static class JobCommands
{
    /// <summary>Runs the verb family <paramref name="args"/> names.</summary>
    /// <param name="args">The command line, 'services …' or 'jobs …'.</param>
    /// <param name="api">The API client and where the verbs write.</param>
    /// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not.</param>
    /// <param name="clock">Times the polling fallback and the wait for a cancel.</param>
    /// <param name="interrupt">The first and the second Ctrl-C.</param>
    /// <returns>The exit code.</returns>
    internal static Task<int> Run(string[] args, (ControlPlaneClient Client, Output Output) api, Func<bool, CancellationToken, Task<Error?>> ensureRunning, TimeProvider clock,
        (CancellationToken Interrupt, CancellationToken Again) interrupt) =>
        args is ["jobs", .. var jobs]
            ? new JobVerbs(api.Client, api.Output, ensureRunning).Run(jobs, interrupt.Interrupt)
            : new ServiceVerbs(api.Client, api.Output, new JobFollower(api.Client, api.Output, clock), ensureRunning).Run(args[1..], interrupt.Interrupt, interrupt.Again);
}
