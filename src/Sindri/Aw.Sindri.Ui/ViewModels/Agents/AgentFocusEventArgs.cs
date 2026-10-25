namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page should put keyboard focus somewhere: a row, its Focus line, or a re-read took focus away with the old rows.</summary>
/// <param name="key">The row's key (a live row's ref, an ended row's key).</param>
/// <param name="toFocusLine">True to land on the row's Focus line (the O key on a row with no Step to open) rather than the row.</param>
/// <param name="onlyIfLost">True when a re-read asks: only if focus was lost with the old rows.</param>
internal sealed class AgentFocusEventArgs(string key, bool toFocusLine, bool onlyIfLost) : EventArgs
{
    /// <summary>The row's key.</summary>
    public string Key { get; } = key;

    /// <summary>True to land on the Focus line.</summary>
    public bool ToFocusLine { get; } = toFocusLine;

    /// <summary>True when only a lost focus is wanted back.</summary>
    public bool OnlyIfLost { get; } = onlyIfLost;
}
