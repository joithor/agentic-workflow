namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What a row is about, as served: a kind (decision, service, profile) and an id.</summary>
/// <param name="Kind">The subject's kind.</param>
/// <param name="Id">Its id (a profile row's is the live hash).</param>
internal sealed record NeedsYouSubject(string Kind, string Id)
{
    /// <summary>The rule for "is this a decision row", in one place: the decision's id when the subject's kind is a decision, else null. A row's own kind word is a label, never the test.</summary>
    internal string? DecisionId => string.Equals(Kind, NeedsYouKinds.Decision, StringComparison.Ordinal) ? Id : null;
}
