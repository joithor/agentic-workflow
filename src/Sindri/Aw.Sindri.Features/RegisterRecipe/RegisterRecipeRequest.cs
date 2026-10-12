using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.RegisterRecipe;

/// <summary>Register a recipe by hash (engine spec §6.3 'recipe register', POST /workflow/recipes; desk). It is validated first; a failing recipe is SND-RECIPE-001.</summary>
/// <param name="Text">The recipe file's text.</param>
/// <param name="File">The file name its findings name.</param>
public sealed record RegisterRecipeRequest(string Text, string File) : ICommand<RegisterRecipeResponse>;
