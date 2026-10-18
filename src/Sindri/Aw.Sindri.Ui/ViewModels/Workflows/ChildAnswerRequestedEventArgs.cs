namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A Workflows row asked to follow its "Answer in the child".</summary>
/// <param name="rowAddress">The row's address, the opener Esc returns to.</param>
/// <param name="answer">The pointer.</param>
internal sealed class ChildAnswerRequestedEventArgs(string rowAddress, ChildAnswerViewModel answer) : EventArgs
{
    /// <summary>The row's address.</summary>
    public string RowAddress { get; } = rowAddress;

    /// <summary>The pointer.</summary>
    public ChildAnswerViewModel Answer { get; } = answer;
}
