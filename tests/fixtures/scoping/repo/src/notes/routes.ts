import { requireRole } from "../roles/guard";
import { deleteNote, getNote, listNotes, saveNote } from "./service";

export const notesRoutes = [
  { method: "GET", path: "/api/notes", handler: requireRole("viewer", (req) => listNotes(req.teamId)) },
  { method: "GET", path: "/api/notes/:id", handler: requireRole("viewer", (req) => getNote(req.params.id)) },
  { method: "PUT", path: "/api/notes/:id", handler: requireRole("editor", (req) => saveNote(req.user, req.body)) },
  { method: "DELETE", path: "/api/notes/:id", handler: requireRole("owner", (req) => deleteNote(req.user, req.params.id)) },
];
