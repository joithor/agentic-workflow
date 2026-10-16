using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// The pre-run refusal of a statement that writes: a clear early error before anything connects. It is not the safeguard (the
/// connection is); a function that writes and nextval pass it on purpose.
/// </summary>
public static class WriteStatements
{
    /// <summary>Checks one query's text.</summary>
    /// <param name="sql">The SQL text of one query.</param>
    /// <returns>Success, or AW-EVIDENCE-020 naming the statement kind that is refused.</returns>
    public static Result<Unit> Check(string sql) => throw new NotSupportedException("WriteStatements.Check is not written yet");
}
