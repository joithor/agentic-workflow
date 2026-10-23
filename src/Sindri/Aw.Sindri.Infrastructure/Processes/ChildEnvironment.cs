using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Processes;

/// <summary>Stub (red): the one factory every ProcessStartInfo in src/Sindri goes through.</summary>
internal static class ChildEnvironment
{
    internal static ProcessStartInfo Start(string fileName, IEnumerable<string>? arguments = null, IReadOnlyDictionary<string, string?>? extra = null)
        => throw new NotSupportedException();

    internal static ProcessStartInfo Build(string fileName, IEnumerable<string>? arguments, IReadOnlyDictionary<string, string?>? extra, IReadOnlyDictionary<string, string> parent)
        => throw new NotSupportedException();
}
