namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One link of the node panel: the other node and the way it links; a click jumps to it.</summary>
internal sealed class NodeEdgeRowViewModel
{
    private readonly Action<string> _jump;

    internal NodeEdgeRowViewModel(string key, string text, Action<string> jump)
    {
        (Key, Text, _jump) = (key, text, jump);
    }

    /// <summary>The other node's key.</summary>
    internal string Key { get; }

    /// <summary>The row's words.</summary>
    public string Text { get; }

    /// <summary>Jumps to the other node.</summary>
    internal void Jump() => _jump(Key);
}
