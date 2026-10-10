namespace Aw.Packets.App.Infrastructure;

/// <summary>Packets's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Packets keeps its state: $AW_STATE_DIR/packets, or ~/.agentic-workflow/packets.</param>
internal sealed record PacketsSettings(string StateDirectory);
