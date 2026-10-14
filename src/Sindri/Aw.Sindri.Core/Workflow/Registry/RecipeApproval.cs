using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// One approval row (spec §4.1, W8), keyed by (id, version, hash) and never written into the recipe file. A drift
/// approval appends a new row for the same key with the new pins.
/// </summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The approved hash.</param>
/// <param name="Approver">The actor who approved it ('human:cli'; the freeze's actor for 'freeze').</param>
/// <param name="ApprovedAt">When.</param>
/// <param name="Via">Desk or freeze.</param>
/// <param name="Base">The base of a generated recipe, or null.</param>
/// <param name="Pins">The block and child pins.</param>
public sealed record RecipeApproval(RecipeId Id, int Version, RecipeHash Hash, string Approver, DateTimeOffset ApprovedAt, ApprovalVia Via, RecipeRef? Base, RecipePins Pins);
