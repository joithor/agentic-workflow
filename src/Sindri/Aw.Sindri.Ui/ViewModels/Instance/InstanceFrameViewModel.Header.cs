using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame's header words (<see cref="InstanceFrameViewModel"/>, continued): the workflow's name up front, the ids in Details.</summary>
internal sealed partial class InstanceFrameViewModel
{
    /// <summary>The workflow's display name: as served, else the recipe id without its "workflow." prefix, title-cased.</summary>
    public string WorkflowName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Details block: the address, the recipe id and hash, and the version. Never the headline.</summary>
    public string DetailsText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Copy of the address, which lives in Details.</summary>
    public CopyAction? AddressCopy { get; private set => Set(ref field, value); }

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The Details disclosure's heading.</summary>
    public string DetailsHeading => Labels.DetailsHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>The accessible name of Copy in Details.</summary>
    public string CopyAddressName => Labels.CopyAddressName(Address);

    /// <summary>Copies the address.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyAddress(CancellationToken cancellationToken) => AddressCopy?.Run(Address, cancellationToken) ?? Task.CompletedTask;

    // NOTE: replace with WorkflowNames once B2 merges
    private static string WorkflowNameOf(InstanceDetail detail)
    {
        var served = detail.Recipe.DisplayName is { Length: > 0 } fromRecipe ? fromRecipe : detail.DisplayName;
        if (!string.IsNullOrEmpty(served))
        {
            return served;
        }

        var id = detail.Recipe.Id;
        var rest = (id.StartsWith("workflow.", StringComparison.Ordinal) ? id["workflow.".Length..] : id).Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Trim();
        return rest.Length == 0 ? id : char.ToUpper(rest[0], CultureInfo.InvariantCulture) + rest[1..];
    }
}
