import { newToken } from "./token";
import { getNote } from "../notes/service";
import { record } from "../audit/audit-log";

export interface SharedLink {
  id: string;
  noteId: string;
  token: string;
  createdBy: string;
  createdAt: string;
}

const links = new Map<string, SharedLink>();

export function createLink(actor: string, noteId: string): SharedLink {
  const link = { id: `lnk_${links.size + 1}`, noteId, token: newToken(), createdBy: actor, createdAt: new Date().toISOString() };
  links.set(link.id, link);
  record(actor, "link.create", link.id);
  return link;
}

// A link stays valid until its note is deleted (decision 0001).
export function resolveLink(token: string) {
  const link = [...links.values()].find((l) => l.token === token);
  return link ? getNote(link.noteId) : undefined;
}
