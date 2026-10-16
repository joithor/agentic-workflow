import { api } from "../api-client";

// The Share dialog: makes a public link for a note and shows it. With the linkRevocation flag it also lists the
// note's links with a Revoke button.
export async function ShareDialog(noteId: string, revocation: boolean) {
  const link = await api<{ id: string; token: string }>("POST", "/api/links", { noteId });
  return {
    title: "Share note",
    url: `/s/${link.token}`,
    note: "Anyone with the link can read this note.",
    actions: revocation ? [{ label: "Revoke", run: () => api("DELETE", `/api/links/${link.id}`) }] : [],
  };
}
