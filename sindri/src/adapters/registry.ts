import type { Deps } from "../deps.js";
import type { LoadedProfile } from "../profile/load.js";
import { makePlanFileTracker } from "./plan-file/tracker.js";
import type { Tracker } from "./types.js";

// Static registry (spec §11.2): the profile picks an adapter by `type:`.
export function makeTracker(loaded: LoadedProfile, deps: Deps): Tracker {
  const t = loaded.profile.tracker;
  return makePlanFileTracker({ repoPath: loaded.repos[t.repo].path, glob: t.glob, include: t.include, git: deps.git });
}
