using System.Globalization;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// One app per user (UI spec §15): the first launch claims the instance and runs the app; a later one hands its message
/// ("focus", or the deep link it was started with) to the running instance and exits. The running instance only
/// navigates on it.
/// </summary>
internal static class SingleLaunch
{
    private const string Scheme = "sindri://";

    /// <summary>Runs the app, or hands over to the running one.</summary>
    /// <param name="instance">This launch's instance port.</param>
    /// <param name="args">The command line.</param>
    /// <param name="runApp">Runs the app until it quits; returns its exit code.</param>
    /// <param name="errors">Standard error.</param>
    /// <returns>The exit code: the app's (2 when it failed), 0 when the running instance handled the message, 1 when it holds the claim but doesn't answer.</returns>
    internal static int Run(ISingleInstance instance, IReadOnlyList<string> args, Func<int> runApp, TextWriter errors)
    {
        // Before the UI starts, on the main thread with no synchronization context: blocking here deadlocks nothing.
        if (Claim(instance))
        {
            return RunApp(runApp, errors);
        }

        if (instance.Send(MessageFor(args), CancellationToken.None).AsTask().GetAwaiter().GetResult())
        {
            return 0;
        }

        // No answer: the holder may have just ended (then this launch claims and runs), or it holds the claim without
        // handling messages (a failed start-up, a wedged app), which only the user can end.
        if (Claim(instance))
        {
            return RunApp(runApp, errors);
        }

        var pid = instance.HolderProcessId() is { } id ? id.ToString(CultureInfo.InvariantCulture) : "unknown";
        errors.WriteLine($"sindri-ui: Sindri app is running but not answering (pid {pid}): quit it or kill it");
        return 1;
    }

    /// <summary>What a later launch sends: the deep link it was started with ('sindri://' optional), else "focus".</summary>
    /// <param name="args">The command line.</param>
    /// <returns>The message.</returns>
    internal static string MessageFor(IReadOnlyList<string> args)
    {
        if (args is not [var only])
        {
            return InstanceMessages.Focus;
        }

        var link = only.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ? only[Scheme.Length..] : only;
        return link.Contains('/', StringComparison.Ordinal) && link.Length <= InstanceMessages.MaxLength ? link : InstanceMessages.Focus;
    }

    private static bool Claim(ISingleInstance instance) => instance.TryClaim(CancellationToken.None).AsTask().GetAwaiter().GetResult();

    private static int RunApp(Func<int> runApp, TextWriter errors)
    {
        var exit = runApp();
        if (exit == SindriApp.FailedExitCode)
        {
            errors.WriteLine("sindri-ui: couldn't start (SND-UI-004); the log line above names the cause");
        }

        return exit;
    }
}
