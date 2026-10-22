using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.ManifestRecipes;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Host.Commands;

/// <summary>'recipe manifest' and 'recipe eval --shipped' (setup-once spec §5.3): both run in this process and never start or contact serve.</summary>
internal static partial class RecipeCommandTable
{
    private const string ManifestUsage = "usage: sindri recipe manifest <dir> [--json]";

    /// <summary>'manifest &lt;dir&gt; [--json]': the directory and the flag, in either order.</summary>
    private static Task<int> RunManifest(string[] args, IServiceProvider services, Output output, CancellationToken ct) => args switch
    {
        [var directory] when !IsFlag(directory) => Manifest(directory, json: false, services, output, ct),
        [var directory, "--json"] when !IsFlag(directory) => Manifest(directory, json: true, services, output, ct),
        ["--json", var directory] when !IsFlag(directory) => Manifest(directory, json: true, services, output, ct),
        _ => output.Error(SindriErrors.InvalidInput.With($"recipe manifest needs one directory and takes only --json ({ManifestUsage})")),
    };

    /// <summary>'validate &lt;file&gt; --against &lt;manifest.json&gt; [--json]': the file, the baseline and the flag, the file and the flag in either order around the baseline.</summary>
    private static Task<int> ValidateAgainst(string[] args, IServiceProvider services, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        string[] rest = [.. args.Where(static a => !string.Equals(a, "--json", StringComparison.Ordinal))];
        return rest switch
        {
            [var file, "--against", var manifest] when !IsFlag(file) && !IsFlag(manifest) => Validate(ValidateRecipeRequest.FromPath(file) with { Against = manifest }, json, services, output, ct),
            ["--against", var manifest, var file] when !IsFlag(file) && !IsFlag(manifest) => Validate(ValidateRecipeRequest.FromPath(file) with { Against = manifest }, json, services, output, ct),
            _ => output.Error(SindriErrors.InvalidInput.With($"recipe validate --against needs one recipe file and one manifest file ({ValidateUsage})")),
        };
    }

    /// <summary>'eval --shipped [--json]'.</summary>
    private static Task<int> RunEvalShipped(string[] flags, IServiceProvider services, Output output, CancellationToken ct) => flags switch
    {
        ["--shipped"] => EvalShipped(json: false, services, output, ct),
        ["--shipped", "--json"] or ["--json", "--shipped"] => EvalShipped(json: true, services, output, ct),
        _ => output.Error(SindriErrors.InvalidInput.With($"recipe eval --shipped takes only --json ({EvalUsage})")),
    };

    private static async Task<int> Manifest(string directory, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        Result<RecipeManifest> result;
        try
        {
            result = await services.GetRequiredService<IService<ManifestRecipesRequest, RecipeManifest>>()
                .Handle(new ManifestRecipesRequest(directory), ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in reading is a tool error (exit 2), not a crash
        {
            return await ToolError(services, output, "recipe manifest", e, directory).ConfigureAwait(false);
        }

        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var code = result.Value.Invalid.Count == 0 ? 0 : 1;
        return json ? await output.Json(result.Value, code).ConfigureAwait(false) : await output.Text(RecipeText.Manifest(result.Value), code).ConfigureAwait(false);
    }

    private static async Task<int> EvalShipped(bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        Result<EvalShippedResponse> result;
        try
        {
            result = await services.GetRequiredService<IService<EvalShippedRequest, EvalShippedResponse>>()
                .Handle(new EvalShippedRequest(), ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in folding is a tool error (exit 2), not a crash
        {
            return await ToolError(services, output, "recipe eval --shipped", e, "the embedded recipes").ConfigureAwait(false);
        }

        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var code = result.Value.Passed ? 0 : 1;
        return json ? await output.Json(result.Value, code).ConfigureAwait(false) : await output.Text(RecipeText.EvalShipped(result.Value), code).ConfigureAwait(false);
    }
}
