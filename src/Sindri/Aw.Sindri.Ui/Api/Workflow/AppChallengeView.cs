namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The challenge the app answers (setup-once spec §4): good for five minutes, held or typed.</summary>
/// <param name="Challenge">The challenge id the answer names.</param>
/// <param name="Action">The action text it is bound to.</param>
/// <param name="ExpiresAt">When it stops working.</param>
/// <param name="Confirm">'held' or 'typed'.</param>
/// <param name="MinHoldMs">For a held one, the shortest hold the server accepts.</param>
/// <param name="TypedAlternative">For a held one, the text that redeems it instead.</param>
internal sealed record AppChallengeView(string Challenge, string Action, DateTimeOffset ExpiresAt, string Confirm, int? MinHoldMs = null, string? TypedAlternative = null);
