namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The head's older-rules line (<see cref="ContextPageViewModel"/>, continued; #1153). Stub: red until the status is read.</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The line by the head: the map was built under older map rules and a refresh rebuilds it.</summary>
    public string RulesOlderText { get; } = string.Empty;

    /// <summary>Whether the older-rules line shows.</summary>
    public bool ShowRulesOlder => ShowWhole && RulesOlderText.Length > 0;
}
