namespace Aw.Sindri.Core.Jobs;

/// <summary>
/// The job whose work is running now: ambient (AsyncLocal) like <see cref="Commands.ICallerContextAccessor"/>, so a service that already
/// takes an <see cref="IProgress{T}"/> (the context engine's map build) passes <see cref="JobContext.Progress"/> on without any change to its request.
/// Outside a job it is null and the service passes null.
/// </summary>
public interface IJobContextAccessor
{
    /// <summary>The job of the code running now, or null.</summary>
    public JobContext? Current { get; }

    /// <summary>Makes <paramref name="context"/> current for the code that follows (and the async calls it starts).</summary>
    /// <param name="context">The job.</param>
    /// <returns>Restores the previous context when disposed.</returns>
    public IDisposable Enter(JobContext context);
}
