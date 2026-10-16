import { requireRole } from "../roles/guard";
import { createLink, resolveLink } from "./links";

export const sharingRoutes = [
  { method: "POST", path: "/api/links", handler: requireRole("editor", (req) => createLink(req.user, req.body.noteId)) },
  { method: "GET", path: "/s/:token", handler: (req) => resolveLink(req.params.token) },
];
