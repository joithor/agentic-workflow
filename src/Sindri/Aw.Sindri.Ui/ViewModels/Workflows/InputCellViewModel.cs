using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One declared input of a Workflows row (screens §4.2): its label with a check when present or a cross when missing.</summary>
internal sealed class InputCellViewModel
{
    private readonly InputPresence _input;

    internal InputCellViewModel(InputPresence input)
    {
        _input = input;
    }

    /// <summary>The input's served label.</summary>
    public string Label => _input.Label;

    /// <summary>True when the input is present.</summary>
    public bool Present => _input.Present;

    /// <summary>The check or the cross (never the only sign: <see cref="Name"/> says it in words).</summary>
    public string Mark => _input.Present ? Labels.MarkInputPresent : Labels.MarkInputMissing;

    /// <summary>The name screen readers hear: the label and whether it is present.</summary>
    public string Name => $"{_input.Label} {(_input.Present ? Labels.InputPresent : Labels.InputMissing)}";
}
