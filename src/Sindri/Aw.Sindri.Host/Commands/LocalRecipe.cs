using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.ValidateRecipe;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// A recipe file 'recipe approve' was given (engine spec §4.2, W8): validated in this process, with its text (to register
/// it) and the eval files it declares, each of which must be present beside it (P2; P4 runs them at approve).
/// </summary>
/// <param name="Path">The file as given.</param>
/// <param name="Text">Its text.</param>
/// <param name="Id">The recipe id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">Its hash.</param>
/// <param name="Evals">The eval line the preview shows: which eval files are present.</param>
/// <param name="EvalFiles">The eval files themselves, which an agent's approval carries.</param>
internal sealed record LocalRecipe(string Path, string Text, RecipeId Id, int Version, RecipeHash Hash, string? Evals, IReadOnlyList<EvalFile>? EvalFiles = null)
{
    /// <summary>The file name the server's findings name.</summary>
    internal string File => System.IO.Path.GetFileName(Path);

    /// <summary>Reads and validates <paramref name="path"/>: SND-RECIPE-001 when it fails validation, SND-RECIPE-008 when a declared eval file is missing.</summary>
    internal static async Task<Result<LocalRecipe>> Read(string path, IServiceProvider services, CancellationToken ct)
    {
        var text = await services.GetRequiredService<IRecipeFiles>().Read(path, ct).ConfigureAwait(false);
        if (text.IsError)
        {
            return text.Error;
        }

        var validated = await Validate(path, services, ct).ConfigureAwait(false);
        if (validated.IsError)
        {
            return validated.Error;
        }

        if (validated.Value is not { Passed: true, Hash: { } hash, Normalized: { } recipe })
        {
            return WorkflowErrors.RecipeInvalid.With($"{path} doesn't validate; fix each RCP line that sindri recipe validate {path} prints");
        }

        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? ".";
        if (recipe.Evals.FirstOrDefault(e => !System.IO.File.Exists(System.IO.Path.Combine(dir, e))) is { } missing)
        {
            return WorkflowErrors.RecipeEvalsFailing.With(
                $"{path} declares eval {missing}, which is missing; approving needs its evals (fix: sindri recipe eval --scaffold {path} > {missing})");
        }

        var evals = recipe.Evals.Count == 0 ? null
            : string.Create(CultureInfo.InvariantCulture, $"{recipe.Evals.Count} present: {string.Join(", ", recipe.Evals)} (run at approve from P4)");
        List<EvalFile> files = [];
        foreach (var eval in recipe.Evals)
        {
            files.Add(new EvalFile(eval, await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(dir, eval), ct).ConfigureAwait(false)));
        }

        return new LocalRecipe(path, text.Value, recipe.Id, recipe.Version, hash, evals, files);
    }

    /// <summary>SND-RECIPE-009 when the file no longer hashes to <paramref name="previewed"/>: it changed while the human read the preview.</summary>
    internal static async Task<Error?> Changed(LocalRecipe file, RecipeHash previewed, IServiceProvider services, CancellationToken ct)
    {
        var now = await Validate(file.Path, services, ct).ConfigureAwait(false);
        return now.IsSuccess && now.Value.Hash == previewed
            ? null
            : WorkflowErrors.RecipeChallengeStale.With(
                $"{file.Path} changed since the preview ({previewed.Display} then, {(now.IsSuccess ? now.Value.Hash?.Display ?? "invalid" : "unreadable")} now); nothing was approved. Run it again and re-read the preview");
    }

    private static async Task<Result<ValidateRecipeResponse>> Validate(string path, IServiceProvider services, CancellationToken ct) =>
        await services.GetRequiredService<IService<ValidateRecipeRequest, ValidateRecipeResponse>>()
            .Handle(ValidateRecipeRequest.FromPath(path), ct).ConfigureAwait(false);
}
