using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Context.Core.Code;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// What the three context Step inputs hand to a Step. A fill must be a function of the stored state and stay under the runner's caps (256 KiB a section
/// and a typed result, 1 MiB a plan, SND-WF-019 otherwise): a re-fill after a restart, an eviction or a pull has to give the same digest (SND-WF-018 else),
/// and the Step must not fail because a map is big. So the views are made stable first (no clock-relative value) and both parts are bounded here.
/// </summary>
internal static class ContextInputSections
{
    /// <summary>The most bytes of text, and of typed result, one input hands over; three inputs stay under the plan cap.</summary>
    internal const int Budget = 120 * 1024;

    private const string Footer = "[{0} more lines not shown: this section is capped at 120 KiB]\n";

    /// <summary>The honest section for an input that could not read its lens.</summary>
    /// <param name="section">The section name.</param>
    /// <param name="what">What was asked ("context lenses").</param>
    /// <param name="why">The reason (a code and message, or what was missing).</param>
    /// <param name="notes">Notes about arguments that were ignored.</param>
    /// <returns>The section, with no typed result.</returns>
    internal static InjectionSections Unavailable(string section, string what, string why, IReadOnlyList<string> notes) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = Notes(notes) + "The " + what + " are unavailable for this Step (" + why + "); it runs without them.\n" });

    /// <summary>The text and the typed result of a view, each within the budget.</summary>
    /// <param name="section">The section name.</param>
    /// <param name="text">The readable text.</param>
    /// <param name="notes">Notes about arguments that were ignored.</param>
    /// <param name="view">The view, as the route answers it.</param>
    /// <param name="json">The JSON options with the view's type.</param>
    /// <typeparam name="T">The view type.</typeparam>
    /// <returns>The sections; the typed result is left out (and the text says so) when it is over the budget.</returns>
    internal static InjectionSections Filled<T>(string section, string text, IReadOnlyList<string> notes, T view, JsonSerializerOptions json)
    {
        var result = JsonSerializer.SerializeToElement(view, (JsonTypeInfo<T>)json.GetTypeInfo(typeof(T)));
        var left = Encoding.UTF8.GetByteCount(result.GetRawText()) > Budget;
        var body = Notes(notes) + Bound(text) + (left ? "[the typed result is over 120 KiB and is left out]\n" : string.Empty);
        return new(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = body }, left ? null : result);
    }

    /// <summary>The text cut at whole lines to fit the budget, with the number of lines left out.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or its head and a footer.</returns>
    internal static string Bound(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= Budget)
        {
            return text;
        }

        var lines = text.Split('\n');
        var kept = new StringBuilder();
        var used = 0;
        var shown = 0;
        foreach (var line in lines)
        {
            var size = Encoding.UTF8.GetByteCount(line) + 1;
            if (used + size > Budget - 256)
            {
                break;
            }

            _ = kept.Append(line).Append('\n');
            used += size;
            shown++;
        }

        return kept.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, Footer, lines.Length - shown)).ToString();
    }

    private static string Notes(IReadOnlyList<string> notes) => string.Concat(notes.Select(static n => "note: " + n + "\n"));

    /// <summary>The set view without its clock-relative values: 'asOf' is the map's creation, the map's age is unstated, and the sources' instants are left out.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The same stored state, the same bytes at any clock.</returns>
    internal static LensesSetView Stable(LensesSetView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with { AsOf = AsOf(view.Coverage), Coverage = Stable(view.Coverage), Searched = Stable(view.Searched) };
    }

    /// <summary>The history view without its clock-relative values.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The same stored state, the same bytes at any clock.</returns>
    internal static HistoryView Stable(HistoryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with { AsOf = AsOf(view.Coverage), Coverage = Stable(view.Coverage), Searched = Stable(view.Searched) };
    }

    /// <summary>The code view without its clock-relative values.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The same stored state, the same bytes at any clock.</returns>
    internal static CodeView Stable(CodeView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with { AsOf = AsOf(view.Coverage), Coverage = Stable(view.Coverage), Searched = Stable(view.Searched) };
    }

    private static DateTimeOffset AsOf(LensCoverage coverage) => coverage.Map?.Created ?? DateTimeOffset.UnixEpoch;

    private static LensCoverage Stable(LensCoverage coverage) => coverage.Map is { } map ? coverage with { Map = map with { AgeDays = -1 } } : coverage;

    private static LensSearched Stable(LensSearched searched) => searched with { Sources = [.. searched.Sources.Select(static s => s with { AsOf = null })] };
}
