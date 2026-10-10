using Aw.Kernel;

namespace Aw.Architecture.Tests.Fixtures.Good.Core;

public static class GoodErrors
{
    public static readonly ErrorDefinition Invalid = new("AW-GOOD-001", ErrorKind.Invalid, "Good input failed validation");

    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
