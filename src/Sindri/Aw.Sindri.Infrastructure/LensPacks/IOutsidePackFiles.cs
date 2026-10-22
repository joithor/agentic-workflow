namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>Where the packs a repo or the machine supplies keep their files (line spec §5.1: '&lt;packs root&gt;/lenses/&lt;id&gt;/').</summary>
internal interface IOutsidePackFiles
{
    /// <summary>The files of one lens pack under a packs root.</summary>
    /// <param name="packsRoot">The packs root: '&lt;repo&gt;/.agentic-workflow/packs' or '$AW_STATE_DIR/packs'.</param>
    /// <param name="id">The pack id.</param>
    /// <returns>Each file's path inside the pack ('/' separated) and its text; null when there is no such pack.</returns>
    public IReadOnlyDictionary<string, string>? Files(string packsRoot, string id);
}
