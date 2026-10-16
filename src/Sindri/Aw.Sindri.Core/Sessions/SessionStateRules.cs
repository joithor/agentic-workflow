namespace Aw.Sindri.Core.Sessions;

/// <summary>The state rules (collector spec §5.2, §5.3).</summary>
public static class SessionStateRules
{
    /// <summary>The state from the newer of the record's status and the hook file, the transcript's age as the fallback.</summary>
    /// <param name="inputs">The sources.</param>
    /// <returns>The merged state.</returns>
    public static MergedState Merge(StateInputs inputs) => throw new NotSupportedException();
}
