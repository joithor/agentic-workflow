namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One link of the node panel: the other node and the way it links; a click jumps to it.</summary>
internal sealed class NodeEdgeRowViewModel
{
    private readonly Action<string> _jump;

    internal NodeEdgeRowViewModel(string key, string text, string labelText, string kindText, Action<string> jump)
    {
        (Key, Text, LabelText, KindText, _jump) = (key, text, labelText, kindText, jump);
    }

    /// <summary>The other node's key.</summary>
    internal string Key { get; }

    /// <summary>The row's words.</summary>
    public string Text { get; }

    /// <summary>Gets the other node's label (its key when nothing names it).</summary>
    public string LabelText { get; }

    /// <summary>Gets the other node's kind; empty when it can't be told.</summary>
    public string KindText { get; }

    /// <summary>Gets a value indicating whether the kind is known.</summary>
    public bool HasKind => KindText.Length > 0;

    /// <summary>Jumps to the other node.</summary>
    internal void Jump() => _jump(Key);
}
