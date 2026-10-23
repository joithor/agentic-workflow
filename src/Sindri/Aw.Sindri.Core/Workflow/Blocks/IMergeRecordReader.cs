using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Reads an instance's merge record ('babysit.jsonl', PR babysitter spec §4.7) back: the stats' read side of <see cref="IMergeRecord"/>.</summary>
public interface IMergeRecordReader
{
    /// <summary>Reads the record of one instance. A missing file is an empty record; an unreadable line counts as an error and is left out.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, or the error of a run area that can't be read.</returns>
    public ValueTask<Result<MergeRecordRows>> Read(string address, CancellationToken cancellationToken);
}
