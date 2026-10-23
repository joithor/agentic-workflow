using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One thing an open desk has its person review (q65): the fact, what kind of thing it is, and where to open it when the fact's stored value says.</summary>
/// <param name="Fact">The fact key ('fact:design').</param>
/// <param name="Kind">What it is.</param>
/// <param name="Path">The file or packet directory, when the fact's value names one; null (left out of the JSON) otherwise.</param>
/// <param name="Url">The link, when the fact's value names one (a pull request); null (left out of the JSON) otherwise.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeskReview(
    string Fact,
    DeskReviewKind Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url = null);
