using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'LoadChildren' (a local activity; engine P7 plan D2, spec §4.1): the child recipes an instance's approval pinned, each at the
/// hash the pin names, and their own pinned children down to the grandchildren, so a child is created from the hash its parent
/// pinned and a replay sees the same answer. A child the approval didn't pin is left out, so creating it is refused.
/// </summary>
/// <param name="store">The registered recipes.</param>
/// <param name="approvals">The approvals, whose pins name the children.</param>
/// <param name="json">The package's AwJson options (they read the stored text).</param>
internal sealed class ChildActivities(IRecipeStore store, IRecipeApprovals approvals, JsonSerializerOptions json)
{
    /// <summary>Reads the pinned children of <paramref name="recipe"/> and of each of them.</summary>
    /// <param name="recipe">The instance's recipe.</param>
    /// <returns>The pinned children, each once.</returns>
    [Activity("LoadChildren")]
    public async Task<IReadOnlyList<LoadedChild>> LoadChildrenAsync(RecipeVersion recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var found = new Dictionary<Core.Workflow.Ids.RecipeId, LoadedChild>();
        var queue = new Queue<RecipeVersion>([recipe]);
        while (queue.TryDequeue(out var version))
        {
            if (Checked(await approvals.Latest(version.Id, version.Version, version.Hash, cancel).ConfigureAwait(false)) is not { } approval)
            {
                continue;
            }

            foreach (var pin in approval.Pins.Children.Where(p => !found.ContainsKey(p.Recipe)))
            {
                if (Checked(await store.Find(pin.Hash, cancel).ConfigureAwait(false)) is { } registered)
                {
                    found[pin.Recipe] = new LoadedChild(pin.Recipe, pin.Hash, Checked(registered.Read(json)) with { Hash = registered.Hash });
                    queue.Enqueue(new RecipeVersion(registered.Id, registered.Version, registered.Hash));
                }
            }
        }

        return [.. found.Values];
    }

    private static T Checked<T>(Result<T> result) => result.IsError ? throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}") : result.Value;
}
