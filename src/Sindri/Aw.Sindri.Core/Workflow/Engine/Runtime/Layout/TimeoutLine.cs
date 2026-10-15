namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>A state's timeout (E-23).</summary>
/// <param name="Kind">What it does when it runs out.</param>
/// <param name="InSeconds">Whole seconds left.</param>
/// <param name="On">What it runs on, such as 'desk'.</param>
public sealed record TimeoutLine(
    TimeoutKind Kind,
    long InSeconds,
    string On);
