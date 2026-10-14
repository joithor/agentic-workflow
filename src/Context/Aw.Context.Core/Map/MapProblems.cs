using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>The problems found in a map file, each with its key path; the file is all or nothing.</summary>
internal sealed class MapProblems
{
    private const int Shown = 10;
    private readonly List<string> _invalid = [];
    private readonly List<string> _undefined = [];

    public bool Any => _invalid.Count + _undefined.Count > 0;

    public void Invalid(string path, string problem) => _invalid.Add($"{path}: {problem}");

    public void Undefined(string path, string key) => _undefined.Add($"{path}: '{key}' isn't defined in the file");

    /// <summary>SND-CONTEXT-031 when any shape problem exists, otherwise SND-CONTEXT-032; the first ten problems, then a count.</summary>
    public Error ToError()
    {
        var (definition, list, fix) = _invalid.Count > 0
            ? (ContextErrors.MapFileInvalid, _invalid, "fix the named entries and import again")
            : (ContextErrors.MapNodeUndefined, _undefined, "add the node or drop the reference, then import again");
        var more = list.Count > Shown ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"; and {list.Count - Shown} more") : string.Empty;
        return definition.With($"{string.Join("; ", list.Take(Shown))}{more}; nothing was written (fix: {fix})");
    }
}
