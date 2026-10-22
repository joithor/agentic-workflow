namespace Aw.Sindri.Core.LensPacks;

/// <summary>
/// A resolved, pin-checked pack ready to be copied into an agent Step's directory (line spec §5.1, §5.2, LN-WF5): the directory it goes in, which pack it is and the files to write.
/// </summary>
/// <param name="Directory">The folder inside the Step directory: 'style' for a style pack, 'lens' for a lens pack.</param>
/// <param name="PackId">The pack's id, recorded on the run record.</param>
/// <param name="Hash">The pack's hash ('sha256:' and 64 hex digits), recorded on the run record.</param>
/// <param name="Files">Each file's path inside <paramref name="Directory"/> ('/' separated, no '..') and its text.</param>
public sealed record PackBundle(string Directory, LensPackId PackId, string Hash, IReadOnlyDictionary<string, string> Files);
