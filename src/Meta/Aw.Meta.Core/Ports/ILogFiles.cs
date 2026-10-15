using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>Reads gate step logs.</summary>
public interface ILogFiles
{
    /// <summary>Reads the end of a log file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="maxCharacters">How many characters to keep, counted from the end.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text, or AW-META-013 when the file can't be read.</returns>
    public ValueTask<Result<string>> Tail(string path, int maxCharacters, CancellationToken cancellationToken);
}
