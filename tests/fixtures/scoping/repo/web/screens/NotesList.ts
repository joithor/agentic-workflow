import { api } from "../api-client";

export async function NotesList() {
  const notes = await api<{ id: string; title: string }[]>("GET", "/api/notes");
  return { title: "Notes", rows: notes.map((n) => ({ text: n.title, open: `#/notes/${n.id}` })) };
}
