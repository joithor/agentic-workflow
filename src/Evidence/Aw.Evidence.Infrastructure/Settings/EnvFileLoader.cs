namespace Aw.Evidence.Infrastructure.Settings;

#pragma warning disable MA0182 // until cutover unit 1.4 wires it into the settings
/// <summary>
/// Stub (cutover unit 1.4 fills it): layers the deployment's env files under the process environment for run and
/// parity, as the TypeScript runner and doctor.sh did. The files are plain NAME=value lines (comments, an optional
/// export  prefix, one layer of quotes) that are parsed, never sourced, so a value is never executed or expanded. Only the
/// UI_EVIDENCE_* settings of the skill's Configuration section are taken from a file; any other name in it is ignored, so a file can't
/// redirect the state directory, the locks or the loader. A name set in the environment is never overridden, and the first file that
/// sets a name wins over the files after it (the local file, then the committed deployment.env).
/// </summary>
internal static class EnvFileLoader
{
    /// <summary>The environment as the settings see it: the process environment, then <paramref name="files"/> in order for the names they may set.</summary>
    /// <param name="environment">Reads a process environment variable.</param>
    /// <param name="files">The env file paths, highest precedence first.</param>
    /// <param name="readFile">Reads a file's text, or null when it is absent or unreadable (skipped, not an error).</param>
    /// <returns>The layered lookup.</returns>
    internal static Func<string, string?> Overlay(Func<string, string?> environment, IReadOnlyList<string> files, Func<string, string?> readFile) =>
        throw new NotSupportedException("the env file loader is not built yet (cutover unit 1.4)");
}
#pragma warning restore MA0182
