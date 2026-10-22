using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>One workflow in the plan, in words: its name, why it is there, and what it can do. The id, version and hash wait in Details.</summary>
internal sealed class PlanRowViewModel
{
    internal PlanRowViewModel(PlanRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        Name = WorkflowNames.Of(recipe.Id, recipe.DisplayName);
        Reason = Labels.ApproveReason(recipe.Change, recipe.ChangeWord);
        Capabilities = [.. recipe.Capabilities.Select(c => c.Sentence)];
        Detail = string.Create(CultureInfo.InvariantCulture, $"{recipe.Id} · v{recipe.Version} · {Labels.Hash12(recipe.Hash)}");
        AccessibleName = Labels.ApproveRowName(Name, Reason);
    }

    /// <summary>The workflow's display name.</summary>
    public string Name { get; }

    /// <summary>Why it is in the plan.</summary>
    public string Reason { get; }

    /// <summary>What it can do, one sentence each.</summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>The id, version and hash, for the Details disclosure.</summary>
    public string Detail { get; }

    /// <summary>The name a screen reader hears.</summary>
    public string AccessibleName { get; }
}
