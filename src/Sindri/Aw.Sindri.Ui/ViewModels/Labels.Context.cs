using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page (screens spec §4.4). Words shared by the page's stages live here; each stage adds its own words to its own Labels.Context*.cs partial. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page head before any map has been read.</summary>
    internal const string ContextNoMap = "No context map yet";

    /// <summary>The anchor box's name.</summary>
    internal const string ContextAnchor = "Ask or look up";

    /// <summary>The anchor box's hint.</summary>
    internal const string ContextAnchorHint = "Ask a question, or type a topic such as feature:name";

    /// <summary>The head of a read: how many topics it was asked about. The map's version is in the answer's closed disclosure, not up front.</summary>
    /// <param name="anchors">How many anchors were found.</param>
    /// <returns>The head line.</returns>
    internal static string ContextHead(int anchors) =>
        string.Create(CultureInfo.InvariantCulture, $"{anchors} {(anchors == 1 ? "topic" : "topics")}");

    /// <summary>The head of a read that came from a question: how many topics the question found.</summary>
    /// <param name="topics">How many anchors the question resolved to.</param>
    /// <returns>The head line.</returns>
    internal static string ContextFromQuestion(int topics) =>
        string.Create(CultureInfo.InvariantCulture, $"From your question: {topics} {(topics == 1 ? "topic" : "topics")}");

    /// <summary>The closed disclosure under the answer: the links, the coverage, what was searched and the map's version.</summary>
    internal const string ContextMore = "More about this answer";

    /// <summary>The map's version as the disclosure shows it.</summary>
    /// <param name="version">The map version as served, with its commit when it has one.</param>
    /// <returns>"Context map v3@abc1234".</returns>
    internal static string ContextMapLine(string version) => "Context map " + version;

    /// <summary>A draft read's mark on the head.</summary>
    internal const string ContextDraftMark = "draft";

    /// <summary>The word for an anchor the map does not hold.</summary>
    internal const string ContextNotFound = "not found";

    /// <summary>The intent band's name.</summary>
    internal const string ContextIntent = "Why";

    /// <summary>The product band's name.</summary>
    internal const string ContextProduct = "What it covers";

    /// <summary>The code band's name.</summary>
    internal const string ContextCode = "Code";

    /// <summary>The edges list's name.</summary>
    internal const string ContextEdges = "Links";

    /// <summary>The coverage strip's name.</summary>
    internal const string ContextCoverage = "How well it is backed";

    /// <summary>The searched sources' name.</summary>
    internal const string ContextSearched = "What was searched";

    /// <summary>The suggested anchors' name.</summary>
    internal const string ContextSuggestions = "Did you mean";

    /// <summary>The word for an untrusted label.</summary>
    internal const string ContextUntrusted = "untrusted";

    /// <summary>The word for a node not yet confirmed.</summary>
    internal const string ContextUnconfirmed = "unconfirmed";

    /// <summary>The word for an anchor node in a band.</summary>
    internal const string ContextAnchorTag = "topic";

    /// <summary>The word for a model-suggested link.</summary>
    internal const string ContextModelSuggested = "model suggested";

    /// <summary>The word for a suggested file link.</summary>
    internal const string ContextSuggestedFile = "suggested, unconfirmed";

    /// <summary>The word for a conflict.</summary>
    internal const string ContextConflict = "conflict";

    /// <summary>The word for the code band's symbols.</summary>
    internal const string ContextSymbols = "symbols";

    /// <summary>The word for an index that is not built, shown beside the symbols.</summary>
    internal const string ContextIndexNotBuilt = "index not built";

    /// <summary>The word for something the band was cut short of.</summary>
    internal const string ContextTruncated = "more not shown";

    /// <summary>A band's header with its count.</summary>
    /// <param name="name">The band's name.</param>
    /// <param name="count">How many rows it holds.</param>
    /// <returns>The header.</returns>
    internal static string ContextBandHeader(string name, int count) => string.Create(CultureInfo.InvariantCulture, $"{name} \u00b7 {count}");

    /// <summary>The coverage strip's intent bar.</summary>
    /// <param name="covered">Nodes with intent.</param>
    /// <param name="of">Nodes in scope.</param>
    /// <returns>The bar's words.</returns>
    internal static string ContextCoverIntent(int covered, int of) => string.Create(CultureInfo.InvariantCulture, $"intent {covered} of {of} cited");

    /// <summary>The coverage strip's product bar.</summary>
    /// <param name="confirmed">Confirmed nodes.</param>
    /// <param name="of">Nodes in scope.</param>
    /// <returns>The bar's words.</returns>
    internal static string ContextCoverProduct(int confirmed, int of) => string.Create(CultureInfo.InvariantCulture, $"product {confirmed} of {of} confirmed");

    /// <summary>The coverage strip's code bar.</summary>
    /// <param name="implemented">Implemented nodes.</param>
    /// <param name="of">Nodes in scope.</param>
    /// <param name="suggested">Nodes with only a suggested link.</param>
    /// <returns>The bar's words.</returns>
    internal static string ContextCoverCode(int implemented, int of, int suggested) =>
        string.Create(CultureInfo.InvariantCulture, $"code {implemented} of {of} implemented \u00b7 {suggested} suggested");

    /// <summary>The box's refusal of too many anchors.</summary>
    /// <param name="max">The most anchors one read takes.</param>
    /// <returns>The refusal.</returns>
    internal static string ContextTooMany(int max) => string.Create(CultureInfo.InvariantCulture, $"At most {max} topics at a time");
}
