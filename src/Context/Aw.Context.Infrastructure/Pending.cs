namespace Aw.Context.Infrastructure;

/// <summary>Red-commit stubs only: removed when the adapters are built.</summary>
internal static class Pending
{
    public static NotSupportedException Because(params object?[] dependencies) =>
        new(string.Concat("S-4 Infrastructure: not built yet (", dependencies.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), " dependencies)"));
}
