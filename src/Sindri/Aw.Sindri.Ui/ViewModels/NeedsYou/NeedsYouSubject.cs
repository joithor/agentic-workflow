namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What a row is about, as served: a kind (decision, service, profile) and an id.</summary>
/// <param name="Kind">The subject's kind.</param>
/// <param name="Id">Its id (a profile row's is the live hash).</param>
internal sealed record NeedsYouSubject(string Kind, string Id);
