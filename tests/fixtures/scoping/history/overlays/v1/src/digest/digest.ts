import { listNotes } from "../notes/service";
import { send } from "./mailer";
import { record } from "../audit/audit-log";

// The weekly digest: notes changed in the last 7 days, to every member who has not unsubscribed.
export async function sendDigest(teamId: string, subscribers: string[], now = new Date()): Promise<number> {
  const since = new Date(now.getTime() - 7 * 86_400_000).toISOString();
  const changed = listNotes(teamId).filter((n) => n.updatedAt >= since);
  for (const to of subscribers) {
    await send({ to, subject: `${changed.length} notes changed this week`, text: changed.map((n) => `- ${n.title}`).join("\n") });
  }
  record("system", "digest.send", teamId);
  return subscribers.length;
}
