namespace Aw.Sindri.Core.Commands;

/// <summary>
/// The current call's caller (API spec C6): AsyncLocal-based like IHttpContextAccessor, so singletons such as
/// EventFactory read it. Null outside any call (S1's internal writers keep the 'host:sindri' stamp).
/// </summary>
public interface ICallerContextAccessor
{
    /// <summary>The caller of the code running now, or null.</summary>
    public CallerContext? Current { get; }

    /// <summary>
    /// Makes <paramref name="context"/> current for the code that follows (and the async calls it starts). For a
    /// service-class context the command chain gives each command attempt its own command id, replacing the context's.
    /// </summary>
    /// <param name="context">The caller.</param>
    /// <returns>Restores the previous caller when disposed.</returns>
    public IDisposable Enter(CallerContext context);

    /// <summary>
    /// Makes a fresh 'service:&lt;name&gt;' caller current. Its command id is the context's; the command chain gives
    /// each attempt under it its own.
    /// </summary>
    /// <param name="service">The service name, for example 'host'.</param>
    /// <returns>Restores the previous caller when disposed.</returns>
    public IDisposable EnterService(string service);
}
