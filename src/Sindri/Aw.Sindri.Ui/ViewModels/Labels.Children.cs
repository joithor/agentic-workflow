using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the children panel (screens §4.2). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The panel's heading.</summary>
    internal const string ChildrenHeading = "Child runs";

    /// <summary>The words of a recipe that declares no children.</summary>
    internal const string NoChildren = "This workflow has no child runs.";

    /// <summary>The words of a recipe that marks a child state, before any child has started.</summary>
    internal const string NoChildrenYet = "No child runs yet.";

    /// <summary>What follows a park that the engine mirrors from a child to its parent.</summary>
    internal const string MirroredHere = "mirrored here";

    /// <summary>The link that lists every child in Work.</summary>
    internal const string OpenAllInWork = "Open all in Work ▸";

    /// <summary>Open on a child row's item.</summary>
    internal const string OpenChildItem = "Open";

    /// <summary>The line under a group that lists only some of its children.</summary>
    /// <param name="more">How many are not listed.</param>
    /// <returns>"… 4 more".</returns>
    internal static string MoreChildren(int more) => string.Create(CultureInfo.InvariantCulture, $"… {more} more");

    /// <summary>A group's count of finished children.</summary>
    /// <param name="done">How many are done.</param>
    /// <param name="total">How many there are.</param>
    /// <returns>"4 of 9 done".</returns>
    internal static string ChildrenDone(int done, int total) => string.Create(CultureInfo.InvariantCulture, $"{done} of {total} done");

    /// <summary>The panel's heading with the number of children.</summary>
    /// <param name="total">How many children there are.</param>
    /// <returns>"CHILDREN 9".</returns>
    internal static string ChildrenCount(int total) => string.Create(CultureInfo.InvariantCulture, $"{ChildrenHeading} {total}");

    /// <summary>A child's loop round.</summary>
    /// <param name="round">The served round.</param>
    /// <returns>"r1/3".</returns>
    internal static string RoundOf(Round round) => string.Create(CultureInfo.InvariantCulture, $"round {round.N} of {round.Max}");

    /// <summary>The panel's accessible name.</summary>
    /// <param name="address">The instance.</param>
    /// <returns>The words.</returns>
    internal static string ChildrenName(string address) => $"Child runs of {address}";

    /// <summary>A workstream's heading.</summary>
    /// <param name="stream">The stream's id.</param>
    /// <param name="label">The stream's label.</param>
    /// <returns>"ws-1 UI".</returns>
    internal static string StreamTitle(string stream, string label) => $"{stream} {label}";

    /// <summary>The accessible name of the link that lists a group's children in Work.</summary>
    /// <param name="workflow">The group's workflow display name.</param>
    /// <param name="address">The instance.</param>
    /// <returns>The words.</returns>
    internal static string OpenAllInWorkName(string workflow, string address) => $"Open all {workflow} children of {address} in Work";

    /// <summary>The accessible name of a child action that is not sent from here.</summary>
    /// <param name="label">The action's label.</param>
    /// <param name="address">The child.</param>
    /// <param name="chip">The words of the pending chip, which the name carries because the chip itself is a glyph for a screen reader; empty for none.</param>
    /// <returns>The words.</returns>
    internal static string ChildActionName(string label, string address, string chip = "") => chip.Length > 0 ? $"{label} {address}, {chip}" : $"{label} {address}";
}
