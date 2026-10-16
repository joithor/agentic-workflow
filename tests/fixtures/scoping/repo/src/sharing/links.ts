import { newToken } from "./token";
import { getNote } from "../notes/service";
import { record } from "../audit/audit-log";
import { flags } from "../config/flags";

export interface SharedLink {
  id: string;
  noteId: string;
  token: string;
  createdBy: string;
  createdAt: string;
  revokedAt?: string;
}

const links = new Map<string, SharedLink>();

export function createLink(actor: string, noteId: string): SharedLink {
  const link = { id: `lnk_${links.size + 1}`, noteId, token: newToken(), createdBy: actor, createdAt: new Date().toISOString() };
  links.set(link.id, link);
  record(actor, "link.create", link.id);
  return link;
}

// A link stays valid until its note is deleted (decision 0001) or, with the linkRevocation flag, until it is revoked.
export function resolveLink(token: string) {
  const link = [...links.values()].find((l) => l.token === token);
  if (!link || (flags.linkRevocation && link.revokedAt)) return undefined;
  return getNote(link.noteId);
}

export function revokeLink(actor: string, id: string): SharedLink | undefined {
  const link = links.get(id);
  if (!link) return undefined;
  link.revokedAt = new Date().toISOString();
  record(actor, "link.revoke", id);
  return link;
}
