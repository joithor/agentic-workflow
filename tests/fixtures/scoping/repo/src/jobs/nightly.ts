import { flags } from "../config/flags";
import { sendDigest } from "../digest/digest";
import { purgeTrash } from "./purge-trash";

// Runs at 02:00. The digest goes out on Mondays only.
export async function nightly(teams: { id: string; subscribers: string[] }[], now = new Date()): Promise<void> {
  for (const team of teams) {
    if (flags.digest && now.getUTCDay() === 1) await sendDigest(team.id, team.subscribers, now);
    purgeTrash(team.id, now);
  }
}
