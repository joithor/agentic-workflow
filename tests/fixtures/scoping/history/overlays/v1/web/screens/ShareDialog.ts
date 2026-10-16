import { api } from "../api-client";

// The Share dialog: makes a public link for a note and shows it.
export async function ShareDialog(noteId: string) {
  const link = await api<{ id: string; token: string }>("POST", "/api/links", { noteId });
  return { title: "Share note", url: `/s/${link.token}`, note: "Anyone with the link can read this note." };
}
