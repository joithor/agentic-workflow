namespace Aw.CodeIndex.Core.Planning;

/// <summary>What <see cref="IndexContinuationDecision"/> tells the main-move service to do with a repo this tick.</summary>
public enum ContinuationAction
{
    /// <summary>Decide as usual.</summary>
    Pass,

    /// <summary>Do not call the shared decision this tick: nothing starts.</summary>
    Suppress,

    /// <summary>Call the shared decision with the recorded head withheld, so a continuation starts like a moved head.</summary>
    WithholdRecorded,
}
