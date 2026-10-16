namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// Tests that move keyboard focus and capture what it draws. They dispatch onto the one shared UI thread, and a test's waits and
/// Dispatcher.RunJobs calls run another class's queued UI work in the middle of it, so these classes run alone, after the parallel ones.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HeadlessWindows
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Headless windows";
}
