import { Note } from "./model";
import { record } from "../audit/audit-log";

const notes = new Map<string, Note>();

export function saveNote(actor: string, note: Note): Note {
  notes.set(note.id, note);
  record(actor, "note.save", note.id);
  return note;
}

export function getNote(id: string): Note | undefined {
  return notes.get(id);
}

export function listNotes(teamId: string): Note[] {
  return [...notes.values()].filter((n) => n.teamId === teamId);
}

export function deleteNote(actor: string, id: string): void {
  notes.delete(id);
  record(actor, "note.delete", id);
}
