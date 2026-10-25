using System.Globalization;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page's concept band, the first layer of the map.</summary>
internal static partial class Labels
{
    /// <summary>The head over the concept layer.</summary>
    internal const string ContextConceptsHead = "Context · concepts";

    /// <summary>The first step of the breadcrumb, and the layer's accessible name.</summary>
    internal const string ContextConceptsCrumb = "Concepts";

    /// <summary>The list of concepts' accessible name.</summary>
    internal const string ContextConceptsListName = "The product's concepts";

    /// <summary>The button that goes up one layer.</summary>
    internal const string ContextConceptsBack = "Back";

    /// <summary>The name of the concept of the unassigned nodes when the server gives none.</summary>
    internal const string ContextConceptOtherName = "Other";

    /// <summary>The mark on a concept a person set.</summary>
    internal const string ContextConceptSetByPerson = "Set by a person";

    /// <summary>The line when the map has no concepts yet; the map itself opens as before.</summary>
    internal const string ContextConceptsEmpty = "The map has no concepts yet, so the whole map is shown.";

    /// <summary>The line while the concepts are still being built; the map itself opens as before.</summary>
    internal const string ContextConceptsBuilding = "The concepts are still being built, so the whole map is shown.";

    /// <summary>The line when the concepts cannot be read; the map itself opens as before.</summary>
    /// <param name="code">The error code.</param>
    /// <returns>"Concepts are not available (SND-CONTEXT-041), so the whole map is shown."</returns>
    internal static string ContextConceptsUnavailable(string code) => $"Concepts are not available ({code}), so the whole map is shown.";

    /// <summary>What a concept holds, in people's words.</summary>
    /// <param name="size">The counts.</param>
    /// <returns>"4 features · 3 records · 2 decisions · 40 code files"; a kind with none is left out, and "nothing yet" when all are none.</returns>
    internal static string ContextConceptSize(ConceptSize size)
    {
        ArgumentNullException.ThrowIfNull(size);
        var parts = new (string Kind, int Count)[] { ("feature", size.Features), ("record", size.Records), ("decision", size.Decisions), ("code", size.Code) }
            .Where(p => p.Count > 0).Select(p => ContextWholeKindCount(p.Kind, p.Count)).ToList();
        return parts.Count == 0 ? "nothing yet" : string.Join(" · ", parts);
    }

    /// <summary>A concept's accessible name.</summary>
    /// <param name="name">The concept's name.</param>
    /// <param name="size">The size line.</param>
    /// <returns>"Self scheduling, 4 features. Press Enter to open."</returns>
    internal static string ContextConceptTileName(string name, string size) => string.Create(CultureInfo.InvariantCulture, $"{name}, {size}. Press Enter to open.");
}
