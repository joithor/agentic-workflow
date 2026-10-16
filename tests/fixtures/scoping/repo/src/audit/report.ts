import { requireRole } from "../roles/guard";
import { allEntries } from "./audit-log";

// Counts audit entries per action over a date range; owners only.
export function auditReport(from: string, to: string): Record<string, number> {
  const counts: Record<string, number> = {};
  for (const e of allEntries()) {
    if (e.at >= from && e.at <= to) counts[e.action] = (counts[e.action] ?? 0) + 1;
  }
  return counts;
}

export const auditRoutes = [
  { method: "GET", path: "/api/audit/report", handler: requireRole("owner", (req) => auditReport(req.query.from, req.query.to)) },
];
