namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>One judge type of review comment and how the babysitter dealt with each (PR babysitter spec §4.7 'comment' rows).</summary>
/// <param name="Type">The judge type ('none' for a row that records none).</param>
/// <param name="Fixed">Fixed by a commit.</param>
/// <param name="Replied">Answered with a reply.</param>
/// <param name="Reacted">Answered with a reaction.</param>
/// <param name="Held">Held for the owner.</param>
/// <param name="Skipped">Skipped.</param>
public sealed record CommentTypeStats(string Type, int Fixed, int Replied, int Reacted, int Held, int Skipped);
