using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Map;

/// <summary>The contract bases' one check: a broken expectation throws ContractViolationException (no Assert in test support).</summary>
internal static class Expect
{
    public static void That(bool holds, string test, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{test}: {what}");
        }
    }

    public static T Ok<T>(Aw.Kernel.Result<T> result, string test) =>
        result.IsError ? throw new ContractViolationException($"{test}: {result.Error.Code} {result.Error.Message}") : result.Value;
}
