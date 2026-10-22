using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'recipe.registration.completed' payload (setup-once spec §7.5): the shipped recipes were registered at start. Key 'recipe.registration.completed:&lt;epoch&gt;'.</summary>
/// <param name="CommandId">The id of the registration run.</param>
/// <param name="Registered">Recipes registered now.</param>
/// <param name="Unchanged">Recipes already registered at this hash.</param>
/// <param name="Failed">Recipes that could not be registered.</param>
public sealed record RecipeRegistrationCompleted(CommandId CommandId, int Registered, int Unchanged, int Failed);
