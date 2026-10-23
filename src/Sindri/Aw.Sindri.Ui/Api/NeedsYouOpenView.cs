namespace Aw.Sindri.Ui.Api;

/// <summary>The 'open' target of a needs-you row (q65): the one place the owner acts on it. The app does not act on it yet.</summary>
/// <param name="Kind">"instance", "questions", "decision", "service", "job", "profile", "recipe" or "setup".</param>
/// <param name="Id">What it opens, by id.</param>
/// <param name="Route">The deep link ("decision/DEC-…", "workflow/&lt;address&gt;", "service/&lt;name&gt;", "recipe/&lt;id&gt;", "needs-you/&lt;rowId&gt;", "setup").</param>
/// <param name="Label">The button's short words.</param>
/// <param name="Instance">The instance the target belongs to when that isn't <paramref name="Id"/>; null (omitted) otherwise.</param>
internal sealed record NeedsYouOpenView(
    string Kind,
    string Id,
    string Route,
    string Label,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Instance = null);
