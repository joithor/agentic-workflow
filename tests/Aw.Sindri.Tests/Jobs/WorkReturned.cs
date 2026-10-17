using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>Makes "the work has returned and the job is parked on its pending cancel" a fact before a test lets the cancel settle (API P6 review 3, I1).</summary>
internal static class WorkReturned
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);   // a hang guard: the work never returns only in a red run

    private static readonly TimeSpan _grace = TimeSpan.FromMilliseconds(150);   // the settle grace: what is left after the work returns is a few synchronous continuations; a runner that ended the job meanwhile shows in far less

    /// <summary>
    /// Blocks until the work has left its handler, then until the job ended or the grace passed. A runner that waits for the pending cancel never ends the
    /// job in the grace (the full grace is spent); one that does not, ends it with the mark still set, which is the lost terminal event the caller's test looks for.
    /// </summary>
    /// <param name="returned">Completes when the work leaves its handler.</param>
    /// <param name="ended">True once the job has left the runner.</param>
    public static void Await(Task returned, Func<bool> ended)
    {
        _ = returned.Wait(_bound, TestContext.Current.CancellationToken);
        _ = SpinWait.SpinUntil(ended, _grace);
    }

    /// <summary>Blocks until the work has left its handler, then for the grace (the run's next steps are asynchronous continuations with nothing to observe).</summary>
    /// <param name="returned">Completes when the work leaves its handler.</param>
    public static void Await(Task returned)
    {
        _ = returned.Wait(_bound, TestContext.Current.CancellationToken);
        RealTime.Sleep(_grace);
    }
}
