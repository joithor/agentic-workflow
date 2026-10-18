using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>One input of the chosen recipe as the form asks for it: a box, a link, JSON, or a file.</summary>
internal sealed class StartFieldViewModel : ObservableObject
{
    /// <summary>The most bytes a fact may hold (API: 1 MiB).</summary>
    internal const int MaxBytes = 1024 * 1024;

    /// <summary>The field for one declared input.</summary>
    /// <param name="input">The input as the recipe serves it.</param>
    /// <param name="files">Where a file is chosen (read when a file is asked for).</param>
    internal StartFieldViewModel(RecipeInputView input, Func<IFilePicker> files)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(files);
        Key = input.Key;
    }

    /// <summary>The input's key ('fact:spec').</summary>
    public string Key { get; }

    /// <summary>The label shown: the recipe's, else the key.</summary>
    public string Label { get; } = string.Empty;

    /// <summary>The words shown above the field, which name it for assistive technology too: the label, and "(required)" when the form cannot send without it.</summary>
    public string Caption { get; } = string.Empty;

    /// <summary>How it is asked for.</summary>
    public StartFieldKind Kind { get; }

    /// <summary>True for a text box.</summary>
    public bool IsText => Kind == StartFieldKind.Text;

    /// <summary>True for a link box.</summary>
    public bool IsLink => Kind == StartFieldKind.Link;

    /// <summary>True for JSON text.</summary>
    public bool IsPayload => Kind == StartFieldKind.Payload;

    /// <summary>True for a file.</summary>
    public bool IsFile => Kind == StartFieldKind.File;

    /// <summary>True when the form cannot send without it: the recipe requires it and does not generate it.</summary>
    public bool IsRequired { get; }

    /// <summary>"generated if empty" for an input the recipe generates; else empty.</summary>
    public string Hint { get; } = string.Empty;

    /// <summary>The typed text.</summary>
    public string Value { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The chosen file in one line, or "No file chosen".</summary>
    public string FileLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The sentence under the field when it cannot be sent, or empty.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Asks for a file and keeps it.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the choice is shown.</returns>
    internal Task Pick(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileLine = string.Empty;
        throw new NotSupportedException();
    }

    /// <summary>What the input gives a start.</summary>
    /// <returns>The fact, none, or why not; the reason is also shown under the field.</returns>
    internal FieldCheck Check()
    {
        Error = string.Empty;
        throw new NotSupportedException();
    }
}
