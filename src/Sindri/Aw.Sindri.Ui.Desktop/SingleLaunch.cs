using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// One app per user (UI spec §15): the first launch claims the instance and runs the app; a later one hands its message
/// ("focus", or the deep link it was started with) to the running instance and exits.
/// </summary>
internal static class SingleLaunch
{
    /// <summary>Runs the app, or hands over to the running one.</summary>
    /// <param name="instance">This launch's instance port.</param>
    /// <param name="args">The command line.</param>
    /// <param name="runApp">Runs the app until it quits; returns its exit code.</param>
    /// <returns>The exit code: the app's, 0 when the running instance took the message, 1 when none answered.</returns>
    internal static int Run(ISingleInstance instance, IReadOnlyList<string> args, Func<int> runApp) => throw new NotSupportedException($"{instance}{args}{runApp.Method.Name}");

    /// <summary>What a later launch sends: the deep link it was started with ('sindri://' optional), else "focus".</summary>
    /// <param name="args">The command line.</param>
    /// <returns>The message.</returns>
    internal static string MessageFor(IReadOnlyList<string> args) => throw new NotSupportedException($"{args}");
}
