using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>What one input gives a start: the fact to send, none when a generated input was left empty, or the sentence that says why it cannot be sent.</summary>
/// <param name="Fact">The fact, or null.</param>
/// <param name="Problem">The reason it cannot be sent, or empty.</param>
internal readonly record struct FieldCheck(StartFact? Fact, string Problem);
