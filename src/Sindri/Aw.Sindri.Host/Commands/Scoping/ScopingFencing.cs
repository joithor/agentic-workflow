using Aw.Context.Core.Trust;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>
/// Fences the stop view's text for a session caller (scoping spec §8.4: "Session callers get untrusted text inside the fence"). Map titles come
/// from the lenses, and acceptance, question and challenger text from models, so every row's text and lines are wrapped in the pack fence;
/// the headings, notes and the Open in Context line are ours and stay plain. A human gets the view as it is.
/// </summary>
internal static class ScopingFencing
{
    private const string Source = "scoping";

    /// <summary>The view with every row's text and lines inside the pack fence.</summary>
    internal static ScopingView Fence(ScopingView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with { Sections = [.. view.Sections.Select(static s => s with { Rows = [.. s.Rows.Select(Fence)] })] };
    }

    private static ScopingRow Fence(ScopingRow row) =>
        row with { Text = UntrustedFence.Wrap(Source, author: null, row.Text), Lines = [.. row.Lines.Select(static l => UntrustedFence.Wrap(Source, author: null, l))] };
}
