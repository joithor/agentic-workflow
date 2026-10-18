using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.RecordEval;

namespace Aw.Sindri.Host.Commands;

internal static partial class RecipeCommandTable
{
    /// <summary>
    /// 'recipe eval --record &lt;address&gt; --out &lt;file&gt;' (spec §6.3): the instance's ledger events written as an eval case, child lines included.
    /// Refuses when the file exists, writing nothing; prints the path it wrote.
    /// </summary>
    private static async Task<int> RecordOut(string address, string file, IServiceProvider services, Output output, CancellationToken ct)
    {
        if (File.Exists(file))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"{file} already exists (fix: choose another --out file, or move it away)")).ConfigureAwait(false);
        }

        Result<RecordEvalResponse> result;
        try
        {
            result = await services.GetRequiredService<IService<RecordEvalRequest, RecordEvalResponse>>()
                .Handle(new RecordEvalRequest(address), ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: a bug in reading or recording is a tool error (W27 exit 2), not a crash
        {
            return await ToolError(services, output, "recipe eval --record", e, address).ConfigureAwait(false);
        }

        if (result.IsError)
        {
            return await output.Error(result.Error).ConfigureAwait(false);
        }

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);   // why: a full path has a directory
            await File.WriteAllTextAsync(file, result.Value.Text, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"can't write the case to {file}: {e.Message} (fix: choose an --out file you can create and write)")).ConfigureAwait(false);
        }

        return await output.Text(file + "\n", 0).ConfigureAwait(false);
    }
}
