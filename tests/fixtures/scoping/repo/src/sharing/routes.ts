import { requireRole } from "../roles/guard";
import { createLink, resolveLink, revokeLink } from "./links";

export const sharingRoutes = [
  { method: "POST", path: "/api/links", handler: requireRole("editor", (req) => createLink(req.user, req.body.noteId)) },
  { method: "GET", path: "/s/:token", handler: (req) => resolveLink(req.params.token) },
  { method: "DELETE", path: "/api/links/:id", handler: requireRole("editor", (req) => revokeLink(req.user, req.params.id)) },
];
