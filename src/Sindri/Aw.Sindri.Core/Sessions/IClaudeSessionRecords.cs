using Aw.Kernel;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Claude Code's session records (collector spec §4.1 step 3): at most 256 files of 64 KB, the '.json' ones only; a '.key' file is never opened.</summary>
public interface IClaudeSessionRecords
{
    /// <summary>Reads every record.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The records; a file that doesn't parse is skipped. An error when the directory can't be listed.</returns>
    public Result<IReadOnlyList<ClaudeRecord>> Read(CancellationToken cancellationToken);
}
