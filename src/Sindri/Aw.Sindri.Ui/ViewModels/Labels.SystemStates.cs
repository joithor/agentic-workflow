using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the System page's connection-state boxes (system spec §3, slice 5).</summary>
internal static partial class Labels
{
    /// <summary>The box over the System page when Sindri is not running.</summary>
    internal const string SystemNotRunningBox = "Sindri is not running";

    /// <summary>The command that starts Sindri, as the not-running box copies it.</summary>
    internal const string SystemServeFix = "sindri serve";

    /// <summary>The box when Sindri answers with something this app can't read.</summary>
    internal const string SystemUnreadableBox = "Sindri is running, but this app can't read its answer";

    /// <summary>The amber line over the last answer once Sindri has stopped answering.</summary>
    /// <param name="seconds">How long Sindri has not answered.</param>
    /// <returns>"Showing the last answer. Sindri has not answered for 30 seconds."</returns>
    internal static string SystemStaleLine(int seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"Showing the last answer. Sindri has not answered for {seconds} seconds.");
}
