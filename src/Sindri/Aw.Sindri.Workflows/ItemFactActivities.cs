using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Engine;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'ReadItemRepo' (engine spec rev 8 §5.1 'Repo', §12 'fact:item'): reads the stored 'fact:item' content and returns its
/// repo, so the interpreter sets the 'Repo' search attribute without I/O in workflow code. The interpreter runs it as a
/// local activity once, on start, and only when the input has a 'fact:item'.
/// </summary>
/// <param name="home">Where the facts are.</param>
internal sealed class ItemFactActivities(WorkflowsHome home)
{
    private static readonly System.Text.Json.JsonSerializerOptions _options = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    /// <summary>The repo 'fact:item' names.</summary>
    /// <param name="item">The stored 'fact:item'.</param>
    /// <returns>Its repo; null when it names none or isn't an item (the attribute stays unset).</returns>
    [Activity("ReadItemRepo")]
    public async Task<string?> ReadRepoAsync(FactValue item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var file = Path.Combine(home.FactsDirectory, Path.GetFileName(item.Sha256));
        var content = await File.ReadAllBytesAsync(file, ActivityExecutionContext.Current.CancellationToken).ConfigureAwait(false);
        var read = AwJson.Deserialize<ItemFact>(content, _options);
        return read.IsError || read.Value.Repo is not { Length: > 0 } repo ? null : repo;
    }
}
