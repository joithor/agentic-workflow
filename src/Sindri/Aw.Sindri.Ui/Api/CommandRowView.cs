namespace Aw.Sindri.Ui.Api;

/// <summary>
/// One row of GET /sindri/commands (API spec §3.3, UI spec E-8, E-16): an action the API knows, with what the app needs
/// to render its control. The app keys buttons by <see cref="Action"/> and decides nothing from the other fields
/// beyond how to draw them.
/// </summary>
/// <param name="Action">The action id: '&lt;Command&gt;' or '&lt;Command&gt;:&lt;variant&gt;'.</param>
/// <param name="Class">The action class (desk, protected, job, …).</param>
/// <param name="AllowedCallers">Who may run it ('human+terminal', 'human', 'session', 'service').</param>
/// <param name="Terminal">True when only a human at a terminal may run it.</param>
/// <param name="Method">The HTTP method, or null for a CLI-local verb.</param>
/// <param name="Route">The route, or null for a CLI-local verb.</param>
/// <param name="Cli">The CLI form, built by the API from checked parts only.</param>
/// <param name="Ui">The UI status: 'ready', 'pending:&lt;phase&gt;' or 'cli-only:&lt;reason&gt;'.</param>
/// <param name="Destructive">True when the app confirms it first.</param>
/// <param name="ReadsViews">True when it waits for the views (refused while they rebuild).</param>
/// <param name="Shortcut">Its default key in the app, or null.</param>
internal sealed record CommandRowView(
    string Action,
    string Class,
    IReadOnlyList<string> AllowedCallers,
    bool Terminal,
    string? Method,
    string? Route,
    string Cli,
    string Ui,
    bool Destructive,
    bool ReadsViews,
    string? Shortcut);
