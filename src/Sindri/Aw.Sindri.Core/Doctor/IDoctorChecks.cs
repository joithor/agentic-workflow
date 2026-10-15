namespace Aw.Sindri.Core.Doctor;

/// <summary>More doctor rows from another part of Sindri (the context engine's, today), appended after the core checks.</summary>
public interface IDoctorChecks
{
    /// <summary>Runs the checks.</summary>
    /// <param name="cancellationToken">Cancels them.</param>
    /// <returns>The rows, in order.</returns>
    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken);
}
