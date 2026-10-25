namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One line of the lens stack: words the app composed, drawn as plain text (a served label is never markup).</summary>
/// <param name="Text">The line.</param>
internal sealed record ContextLine(string Text);
