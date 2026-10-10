using Aw.Kernel.Testing;

namespace Aw.BugFix.Testing;

/// <summary>Contract assertions: a failure names the contract clause that broke.</summary>
internal static class Expect
{
    public static void That(bool condition, string what)
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: {what}");
        }
    }

    public static void Equal<T>(T expected, T actual, string what) =>
        That(EqualityComparer<T>.Default.Equals(expected, actual), $"{what} (expected {expected}, got {actual})");
}
