import { notesRoutes } from "./notes/routes";
import { sharingRoutes } from "./sharing/routes";

export function buildApp() {
  return { routes: [...notesRoutes, ...sharingRoutes] };
}
