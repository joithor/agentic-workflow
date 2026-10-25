namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>Anything on the Agents page that stands for one session (a row): the window finds the row that opened the sheet by this ref, so Esc can return focus to it.</summary>
internal interface ISessionRef
{
    /// <summary>The session's ref, provider/id.</summary>
    public string Ref { get; }
}
