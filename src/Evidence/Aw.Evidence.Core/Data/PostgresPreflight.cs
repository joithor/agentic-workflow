using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>The two checks made on a Postgres server before any script query: it is the one the entry names, and the role can write nothing.</summary>
public static class PostgresPreflight
{
    /// <summary>Compares who the server says it is with the entry's identity.</summary>
    /// <param name="expected">The entry's identity.</param>
    /// <param name="reported">What the server reported.</param>
    /// <returns>Success, or AW-EVIDENCE-021 naming the expected and the reported value.</returns>
    public static Result<Unit> CheckIdentity(ServerIdentity expected, ReportedServer reported) =>
        throw new NotSupportedException("PostgresPreflight.CheckIdentity is not written yet");

    /// <summary>Checks that the role is not a superuser and holds nothing that writes.</summary>
    /// <param name="role">What the server says the role may do.</param>
    /// <returns>Success, or AW-EVIDENCE-020 naming what the role may do.</returns>
    public static Result<Unit> CheckRole(RoleFacts role) =>
        throw new NotSupportedException("PostgresPreflight.CheckRole is not written yet");
}
