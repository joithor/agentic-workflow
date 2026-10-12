namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The desk runtime's settings (engine spec W20): the 'workflow.deskTimeout' floor every desk wait is raised to.
/// 'LoadRecipe' hands it to the interpreter, so the workflow reads it from history. Engine P4 unit (c) registers the 2d
/// default; S1-D11's profile key (unit (e)) sets it.
/// </summary>
/// <param name="Timeout">The 'workflow.deskTimeout' floor.</param>
internal sealed record DeskSettings(TimeSpan Timeout);
