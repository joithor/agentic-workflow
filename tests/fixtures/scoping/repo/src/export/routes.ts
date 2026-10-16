import { requireRole } from "../roles/guard";
import { exportTeam } from "./exporter";

export const exportRoutes = [
  { method: "GET", path: "/api/export", handler: requireRole("owner", (req) => exportTeam(req.teamId, req.query.format ?? "markdown")) },
];
