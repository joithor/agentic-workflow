using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>The meta's running log, a markdown file of one line per event.</summary>
public interface IRunningLog
{
    /// <summary>Appends one line, in a single write, creating the file and its directory when missing.</summary>
    /// <param name="line">The line without its line ending.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-008.</returns>
    public ValueTask<Result<Unit>> Append(string line, CancellationToken cancellationToken);
}
