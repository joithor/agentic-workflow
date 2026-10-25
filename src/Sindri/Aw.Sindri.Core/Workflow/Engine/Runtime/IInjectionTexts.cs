namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The in-memory texts of the open runs' inputs (plan: E3 stays parked). Read only from activities and the pull route,
/// never from workflow code; never persisted and never logged.
/// </summary>
public interface IInjectionTexts
{
    /// <summary>Keeps <paramref name="texts"/>; a newer run of the same instance drops the older runs' entries.</summary>
    /// <param name="key">Whose texts.</param>
    /// <param name="texts">The texts.</param>
    public void Put(InjectionKey key, FilledTexts texts);

    /// <summary>The kept texts.</summary>
    /// <param name="key">Whose texts.</param>
    /// <returns>The texts; null when none are kept (never filled, evicted or the process restarted).</returns>
    public FilledTexts? Get(InjectionKey key);

    /// <summary>Drops every entry of the instance (its Step ended).</summary>
    /// <param name="address">The instance address.</param>
    public void Drop(string address);
}
