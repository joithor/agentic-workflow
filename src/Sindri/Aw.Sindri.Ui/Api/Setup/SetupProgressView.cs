namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>How far a counted step is.</summary>
/// <param name="Done">Units finished.</param>
/// <param name="Total">Units in all.</param>
internal sealed record SetupProgressView(int Done, int Total);
