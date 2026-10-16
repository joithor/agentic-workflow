import { notesRoutes } from "./notes/routes";
import { sharingRoutes } from "./sharing/routes";
import { exportRoutes } from "./export/routes";

export function buildApp() {
  return { routes: [...notesRoutes, ...sharingRoutes, ...exportRoutes] };
}
