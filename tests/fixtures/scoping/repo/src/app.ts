import { flags } from "./config/flags";
import { notesRoutes } from "./notes/routes";
import { sharingRoutes } from "./sharing/routes";
import { exportRoutes } from "./export/routes";
import { auditRoutes } from "./audit/report";

export function buildApp() {
  const routes = [...notesRoutes, ...sharingRoutes, ...exportRoutes];
  if (flags.auditReport) routes.push(...auditRoutes);
  return { routes };
}
