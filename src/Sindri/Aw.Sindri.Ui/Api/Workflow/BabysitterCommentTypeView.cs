namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What was done with comments of one type.</summary>
/// <param name="Type">The type, as served (a type this build doesn't know shows raw).</param>
/// <param name="Fixed">Fixed in code.</param>
/// <param name="Replied">Answered with a reply.</param>
/// <param name="Reacted">Answered with a reaction.</param>
/// <param name="Held">Held for you.</param>
/// <param name="Skipped">Skipped.</param>
internal sealed record BabysitterCommentTypeView(string Type, int Fixed, int Replied, int Reacted, int Held, int Skipped);
