namespace Aw.Kernel.Testing;

/// <summary>The test lanes (spec §4b.5). Each test carries exactly one, as the trait 'Lane'.</summary>
public enum TestLane
{
    /// <summary>Core logic and services with in-memory fakes. No I/O.</summary>
    Unit = 1,

    /// <summary>Repositories on a real temp SQLite file; endpoints through the test host.</summary>
    Integration = 2,

    /// <summary>Real containers, browsers and index builds. Nightly or alone.</summary>
    Heavy = 3,
}
