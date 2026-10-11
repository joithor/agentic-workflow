using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'recipe' verb group's table (spec §6.3; its text is Recipe/RecipeText). 'validate' and 'eval' are local: they run in this process against
/// the build's catalog and never route to serve. Checker exit codes (W27): 0 pass, 1 fail, 2 usage or tool error.
/// </summary>
internal static class RecipeCommandTable
{
    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        ["validate", var file] => Validate(file, json: false, services, output, ct),
        ["validate", var file, "--json"] => Validate(file, json: true, services, output, ct),
        ["eval", var file] => Eval(file, json: false, services, output, ct),
        ["eval", var file, "--json"] => Eval(file, json: true, services, output, ct),
        _ => UnknownVerb(args, output),
    };

    private static async Task<int> Validate(string file, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        var result = await services.GetRequiredService<IService<ValidateRecipeRequest, ValidateRecipeResponse>>()
            .Handle(ValidateRecipeRequest.FromPath(file), ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var response = result.Value;
        var code = response.Passed ? 0 : 1;
        if (json)
        {
            return await output.Json(RecipeValidationView.From(response, services.GetRequiredService<JsonSerializerOptions>()), code).ConfigureAwait(false);
        }

        return await output.Text(RecipeText.Validation(response), code).ConfigureAwait(false);
    }

    private static async Task<int> Eval(string file, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        var result = await services.GetRequiredService<IService<EvalRecipeRequest, EvalRecipeResponse>>()
            .Handle(new EvalRecipeRequest(file), ct).ConfigureAwait(false);
        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var response = result.Value;
        var code = response.Passed ? 0 : 1;
        if (json)
        {
            return await output.Json(response, code).ConfigureAwait(false);
        }

        return await output.Text(RecipeText.Eval(response), code).ConfigureAwait(false);
    }

    private static Task<int> UnknownVerb(string[] args, Output output) =>
        output.Error(SindriErrors.UnknownVerb.With(
            $"'recipe {string.Join(' ', args)}' is not a sindri command (usage: sindri recipe validate|eval <file> [--json])"));
}
