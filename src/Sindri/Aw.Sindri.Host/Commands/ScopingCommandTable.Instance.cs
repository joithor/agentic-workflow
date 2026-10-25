using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Host.Commands;

/// <summary>The instance reads of the scoping verbs: the stop view through API2, and the facts through the facts route.</summary>
internal static partial class ScopingCommandTable
{
    private static async Task<Result<ScopingView>> InstanceView(RecipeDesk desk, string address, CancellationToken ct)
    {
        var instance = await ScopingInstance(desk, address, ct).ConfigureAwait(false);
        if (instance.IsError)
        {
            return instance.Error;
        }

        var read = await desk.Client.Get<ScopingView>($"{InstanceReads.Instances}/{instance.Value}/scoping", ct).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value.Value;
    }

    /// <summary>An instance's facts through the control plane's facts route (class query); a serve that is down is SND-API-001 and is never started.</summary>
    private static async Task<Result<Dictionary<string, string>>> InstanceFacts(RecipeDesk desk, string address, CancellationToken ct)
    {
        var instance = await ScopingInstance(desk, address, ct).ConfigureAwait(false);
        if (instance.IsError)
        {
            return instance.Error;
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in ScopingFactKeys.All)
        {
            var read = await desk.Client.Get<FactView>($"{InstanceReads.Instances}/{instance.Value}/facts/{Uri.EscapeDataString("fact:" + key)}", ct).ConfigureAwait(false);
            if (read.IsError && string.Equals(read.Error.Code, SindriErrors.ItemNotFound.Code, StringComparison.Ordinal))
            {
                continue;   // not produced yet
            }

            if (read.IsError)
            {
                return read.Error;
            }

            facts[key] = read.Value.Value.Body.GetRawText();
        }

        return facts;
    }

    /// <summary>The id of the workflow.scoping instance an address names, through the instances list (class query); a serve that is down is SND-API-001.</summary>
    private static async Task<Result<string>> ScopingInstance(RecipeDesk desk, string address, CancellationToken ct)
    {
        var listed = await desk.Client.Get<Core.Workflow.Views.InstanceList>($"{InstanceReads.Instances}?address={Uri.EscapeDataString(address)}", ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.Value.Instances is not [var instance])
        {
            return Core.Workflow.WorkflowErrors.InstanceNotFound.With($"'{address}' names no one instance (fix: sindri workflow list)");
        }

        if (!string.Equals(instance.Recipe.Id.Value, "workflow.scoping", StringComparison.Ordinal))
        {
            return ScopingErrors.NotAScopingInstance.With($"{address} runs {instance.Recipe.Id.Value} (fix: pass the address of a workflow.scoping instance)");
        }

        return instance.Id.Value;
    }
}
