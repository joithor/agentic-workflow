import { deleteNote, listNotes } from "../notes/service";

// Notes in the trash for more than 30 days are deleted for good.
export function purgeTrash(teamId: string, now: Date): number {
  const cutoff = new Date(now.getTime() - 30 * 86_400_000).toISOString();
  const old = listNotes(teamId).filter((n) => n.title.startsWith("[trash] ") && n.updatedAt < cutoff);
  for (const n of old) deleteNote("system", n.id);
  return old.length;
}
