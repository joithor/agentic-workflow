using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.ManifestRecipes;
using Aw.Sindri.Features.RecordEval;
using Aw.Sindri.Features.ValidateRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>The recipe authoring tools, all in process: validate, eval (one file or the shipped set), manifest and the eval recorder.</summary>
internal static class RecipeToolsServiceCollectionExtensions
{
    /// <summary>Registers the recipe tools' services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecipeTools(this IServiceCollection services) => services
        .AddScoped<IService<ValidateRecipeRequest, ValidateRecipeResponse>, ValidateRecipeService>()
        .AddScoped<IService<EvalRecipeRequest, EvalRecipeResponse>, EvalRecipeService>()
        .AddScoped<IService<EvalShippedRequest, EvalShippedResponse>, EvalShippedRecipesService>()
        .AddScoped<IService<ManifestRecipesRequest, RecipeManifest>, ManifestRecipesService>()
        .AddScoped<IService<RecordEvalRequest, RecordEvalResponse>, RecordEvalService>();
}
