using Aw.Sindri.Ui.Platform;

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
    /// <returns>The exit code: the app's, 0 when the running instance took the message, 1 when none answered.</returns>
    internal static int Run(ISingleInstance instance, IReadOnlyList<string> args, Func<int> runApp)
    {
        // Before the UI starts, on the main thread with no synchronization context: blocking here deadlocks nothing.
        if (instance.TryClaim(CancellationToken.None).AsTask().GetAwaiter().GetResult())
        {
            return runApp();
        }

        if (instance.Send(MessageFor(args), CancellationToken.None).AsTask().GetAwaiter().GetResult())
        {
            return 0;
        }

        Console.Error.WriteLine("sindri-ui: another instance holds the claim but didn't answer; quit it and try again");
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
}
