namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>POST /sindri/setup/repos' body (setup-once spec §5.1 AddRepo, desk): the folder the person chose or typed, as an absolute path. The app never sends a name; Sindri names the repo after the folder.</summary>
/// <param name="Path">The absolute path of the repository's top folder.</param>
internal sealed record AddRepoRequest(string Path);
