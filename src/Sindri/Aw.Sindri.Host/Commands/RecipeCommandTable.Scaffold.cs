using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.EvalRecipe;

namespace Aw.Sindri.Host.Commands;

internal static partial class RecipeCommandTable
{
    /// <summary>
    /// 'recipe eval --scaffold &lt;file&gt; --out &lt;dir&gt;': one '&lt;case&gt;.jsonl' per path through the recipe (a route recipe has
    /// several). Refuses when any of them exists, writing nothing; prints each path it wrote.
    /// </summary>
    private static async Task<int> ScaffoldOut(string file, string directory, IServiceProvider services, Output output, CancellationToken ct)
    {
        Result<EvalRecipeResponse> result;
        try
        {
            result = await services.GetRequiredService<IService<EvalRecipeRequest, EvalRecipeResponse>>()
                .Handle(new EvalRecipeRequest(file, EvalMode.Scaffold), ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in reading or folding is a tool error (W27 exit 2), not a crash
        {
            return await ToolError(services, output, "recipe eval", e, file).ConfigureAwait(false);
        }

        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        var files = (result.Value.Files ?? []).Select(f => (Path: Path.Combine(directory, f.Name + ".jsonl"), f.Text)).ToList();
        if (files.Find(f => File.Exists(f.Path)) is { Path: { } existing })
        {
            return await output.Error(SindriErrors.InvalidInput.With($"{existing} already exists (fix: choose another --out directory, or move it away)")).ConfigureAwait(false);
        }

        _ = Directory.CreateDirectory(directory);
        foreach (var (path, text) in files)
        {
            await File.WriteAllTextAsync(path, text, ct).ConfigureAwait(false);
        }

        return await output.Text(string.Concat(files.Select(static f => f.Path + "\n")), 0).ConfigureAwait(false);
    }
}
