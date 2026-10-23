namespace Aw.Evidence.Core.Settings;

/// <summary>
/// The worktree-slot convention of a deployment (SKILL 'Worktree stacks'): slot N serves the web app on 3000 + PortStep * N and the API
/// on the API port + PortStep * N, and has its own database. All of it is configuration, never code.
/// </summary>
/// <param name="FileName">The gitignored env file in a worktree's root that holds its slot (UI_EVIDENCE_SLOT_FILE).</param>
/// <param name="Key">The name in that file whose value is the slot number (UI_EVIDENCE_SLOT_KEY).</param>
/// <param name="Count">The highest slot, 1 or more (UI_EVIDENCE_SLOT_COUNT).</param>
/// <param name="PortStep">The port step per slot, 1 or more (UI_EVIDENCE_SLOT_PORT_STEP).</param>
/// <param name="Database">The slot's database name with {slot} for its number, or null to skip the database check (UI_EVIDENCE_SLOT_DATABASE).</param>
public sealed record SlotSettings(string FileName, string Key, int Count, int PortStep, string? Database);
