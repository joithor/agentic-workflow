using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>
/// The one place the human acts on a needs-you row (owner requirement q65): the app goes from the row to the action in one click through <see cref="Route"/>.
/// Every row carries exactly one; a row never lacks it. The field name 'open' is a reversible default.
/// </summary>
/// <param name="Kind">What it opens: one of <see cref="NeedsYouOpenKinds"/>.</param>
/// <param name="Id">What it opens, by id: the decision id, the instance address, the service name, the recipe id or the setup step.</param>
/// <param name="Route">The deep link the app parses ('decision/&lt;id&gt;', 'workflow/&lt;address&gt;', 'service/&lt;name&gt;', 'recipe/&lt;id&gt;', 'needs-you/&lt;rowId&gt;', 'setup').</param>
/// <param name="Label">The short words on the button ("Answer the question", "Review and decide").</param>
/// <param name="Instance">The address of the instance the target belongs to when that isn't <paramref name="Id"/> (a parked descendant's row names its root); null (omitted) otherwise.</param>
public sealed record NeedsYouOpen(
    string Kind,
    string Id,
    string Route,
    string Label,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Instance = null);
