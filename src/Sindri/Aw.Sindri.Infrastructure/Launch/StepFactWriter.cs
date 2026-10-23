using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Writes a Step's fact files into its run directory.</summary>
internal static class StepFactWriter
{
    /// <summary>Writes each file under <paramref name="runDirectory"/>, replacing what a retry left there.</summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <param name="files">File name to content.</param>
    /// <returns>An error when a name would leave the directory (nothing is written then); null when every file is written.</returns>
    internal static Error? Write(string runDirectory, IReadOnlyDictionary<string, string> files) => throw new NotSupportedException();
}
