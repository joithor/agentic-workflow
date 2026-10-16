import { listNotes } from "../notes/service";

export type ExportFormat = "markdown" | "json";

export function exportTeam(teamId: string, format: ExportFormat): string {
  const notes = listNotes(teamId);
  if (format === "json") return JSON.stringify(notes, null, 2);
  return notes.map((n) => `# ${n.title}\n\n${n.body}\n`).join("\n");
}
