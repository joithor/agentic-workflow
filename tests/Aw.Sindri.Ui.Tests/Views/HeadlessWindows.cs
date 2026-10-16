namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// Tests that open headless windows and move focus. They all dispatch onto the one shared UI thread, and a test's waits and
/// Dispatcher.RunJobs calls run another class's queued UI work in the middle of it, so these classes run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HeadlessWindows
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Headless windows";
}
