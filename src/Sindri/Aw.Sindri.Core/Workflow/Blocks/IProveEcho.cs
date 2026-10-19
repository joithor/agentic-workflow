namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The files behind the test-only echo input (plan decision 9): the text it serves and the log of its calls.</summary>
public interface IProveEcho
{
    /// <summary>The text the echo input serves.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text; the call fails when there is none.</returns>
    public ValueTask<string> ReadText(CancellationToken cancellationToken);

    /// <summary>Appends one line to the call log. The line never holds the text.</summary>
    /// <param name="line">The line.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The write.</returns>
    public ValueTask Log(string line, CancellationToken cancellationToken);
}
