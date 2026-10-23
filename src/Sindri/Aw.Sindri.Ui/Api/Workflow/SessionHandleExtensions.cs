namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What the app makes of a served <see cref="SessionHandle"/>.</summary>
internal static class SessionHandleExtensions
{
    /// <summary>The session's ref, the form its link and sheet use: provider/id.</summary>
    /// <param name="session">The served handle.</param>
    /// <returns>The ref.</returns>
    internal static string Ref(this SessionHandle session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Provider + "/" + session.Id;
    }
}
