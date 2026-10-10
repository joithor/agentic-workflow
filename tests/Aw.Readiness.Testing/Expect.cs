using Aw.Kernel.Testing;

namespace Aw.Readiness.Testing;

/// <summary>The contract bases' assertions. Like Aw.Kernel.Testing's contracts they throw ContractViolationException
/// naming what the implementation broke, so Testing needs only xunit's extensibility package, not its assert library.</summary>
internal static class Expect
{
    internal static void True(bool condition, string broken)
    {
        if (!condition)
        {
            throw new ContractViolationException(broken);
        }
    }

    internal static void Equal<T>(T expected, T actual, string what) =>
        True(EqualityComparer<T>.Default.Equals(expected, actual), $"{what}: expected '{expected}', got '{actual}'");

    internal static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string what) =>
        True(expected.SequenceEqual(actual), $"{what}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");

    internal static void Evidence(string evidence, string what) =>
        True(!string.IsNullOrWhiteSpace(evidence), $"{what} has no evidence");
}
