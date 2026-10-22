using Aw.Kernel;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Writes a Step's pack bundle into its directory (LN-WF5).</summary>
internal static class PackBundleFiles
{
    /// <summary>Writes the bundle's files under '&lt;stepDirectory&gt;/&lt;bundle.Directory&gt;/', replacing what a retry left there.</summary>
    /// <param name="stepDirectory">The Step directory.</param>
    /// <param name="bundle">The bundle.</param>
    /// <returns>An error when a file's path would leave the bundle's folder; null when every file is written.</returns>
    internal static Error? Write(string stepDirectory, PackBundle bundle) => throw new NotSupportedException();
}
