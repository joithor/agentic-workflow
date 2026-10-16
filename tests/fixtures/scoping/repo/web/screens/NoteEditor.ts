import { api } from "../api-client";

export async function NoteEditor(id: string) {
  const note = await api<{ title: string; body: string }>("GET", `/api/notes/${id}`);
  return {
    title: note.title,
    body: note.body,
    actions: [
      { label: "Save", run: (body: string) => api("PUT", `/api/notes/${id}`, { ...note, body }) },
      { label: "Share", open: `#/notes/${id}/share` },
    ],
  };
}
