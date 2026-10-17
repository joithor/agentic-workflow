#!/usr/bin/env bash
# history/build.sh <parent-dir> — build the line fixture's repo at <parent-dir>/fixture-notes: the scoping fixture's
# history (tests/fixtures/scoping/history/build.sh, same head), plus the branch line/p1 holding the first phase's
# build, with the planted items of the spec's 10.3. Prints the head sha of main on stdout.
#
# Deterministic: author, committer and dates are fixed, git runs with no user, system or hook configuration and none of
# the caller's GIT_* variables, so the shas are the same on every machine (history/golden-head.txt pins them).
#
# The phase P1 build (plan P1 allowedFiles: new migrations/006_link_expiry.sql, src/sharing/links.ts,
# src/sharing/routes.ts; test change tests/sharing.test.ts; new tests are always allowed):
#   line/p1-red          the red commit: new tests, two fail and one passes already.   PLANTED PL-RED
#   line/p1-green        the build: inside allowedFiles.
#   line/p1-out-of-scope a change to src/digest/digest.ts, which P1 may not touch.      PLANTED PL-SCOPE
#   line/p1-canary       a commit message that carries the P1 set's canary phrase.      PLANTED PL-CANARY
# The canary phrase is read from holdout/expiry/P1.set.json at run time, so it appears in this fixture only in the
# hidden set; no secret-shaped string is written here (the scoping build assembles its own fake token).
set -euo pipefail

SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCOPING="$SRC/../scoping"
[ $# -eq 1 ] || { echo "usage: build.sh <parent-dir>" >&2; exit 2; }
PARENT="$1"
DEST="$PARENT/fixture-notes"

CANARY="$(sed -n 's/.*"phrase": "\([^"]*\)".*/\1/p' "$SRC/holdout/expiry/P1.set.json")"
[ -n "$CANARY" ] || { echo "build.sh: no canary phrase in holdout/expiry/P1.set.json" >&2; exit 1; }

BASE_HEAD="$(bash "$SCOPING/history/build.sh" "$PARENT")" || exit $?

for v in $(env | sed -n 's/^\(GIT_[A-Za-z_]*\)=.*/\1/p'); do unset "$v"; done
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
git() { command git -C "$DEST" -c core.hooksPath=/dev/null -c commit.gpgsign=false -c core.autocrlf=false -c init.defaultBranch=main "$@"; }

# commit <iso-date> <message>: commits everything in the working tree as the fixture's builder.
commit() {
  git add -A
  GIT_AUTHOR_NAME="Rowan Sample" GIT_AUTHOR_EMAIL="rowan@fixture.invalid" GIT_COMMITTER_NAME="Rowan Sample" GIT_COMMITTER_EMAIL="rowan@fixture.invalid" \
    GIT_AUTHOR_DATE="$1" GIT_COMMITTER_DATE="$1" git commit -q -m "$2"
}

git checkout -q -b line/p1 main

# --- the red commit: new tests only ---
cat > "$DEST/tests/expiry.test.ts" <<'TS'
import { describe, expect, it } from "vitest";
import { createLink } from "../src/sharing/links";

describe("link expiry (P1)", () => {
  it("refuses an expiry time that is not in the future", () => {
    expect(() => createLink("ana", "n1", { expiresAt: "2000-01-01T00:00:00Z" })).toThrow(/future/);
  });

  it("stores the expiry time on the new link", () => {
    const link = createLink("ana", "n1", { expiresAt: "2999-01-01T00:00:00Z" });
    expect(link.expiresAt).toBe("2999-01-01T00:00:00Z");
  });

  // PLANTED PL-RED
  it("a link made with no expiry still gets a token", () => {
    const link = createLink("ana", "n1");
    expect(link.token).toBeTruthy();
  });
});
TS
commit 2026-10-16T12:00:00Z "test: red tests for the expiry time on create (P1)"
git tag line/p1-red

# --- the green commit: inside allowedFiles ---
printf 'ALTER TABLE shared_links ADD COLUMN expires_at TEXT;\n' > "$DEST/migrations/006_link_expiry.sql"
cat > "$DEST/src/sharing/links.ts" <<'TS'
import { newToken } from "./token";
import { getNote } from "../notes/service";
import { record } from "../audit/audit-log";
import { flags } from "../config/flags";

export interface SharedLink {
  id: string;
  noteId: string;
  token: string;
  createdBy: string;
  createdAt: string;
  revokedAt?: string;
  expiresAt?: string;
}

const links = new Map<string, SharedLink>();

export function createLink(actor: string, noteId: string, options: { expiresAt?: string } = {}): SharedLink {
  const now = new Date();
  if (options.expiresAt !== undefined && new Date(options.expiresAt).getTime() <= now.getTime()) {
    throw new Error("expiresAt must be in the future");
  }
  const link = { id: `lnk_${links.size + 1}`, noteId, token: newToken(), createdBy: actor, createdAt: now.toISOString(), expiresAt: options.expiresAt };
  links.set(link.id, link);
  record(actor, "link.create", link.id);
  return link;
}

// A link stays valid until its note is deleted (decision 0001) or, with the linkRevocation flag, until it is revoked.
export function resolveLink(token: string) {
  const link = [...links.values()].find((l) => l.token === token);
  if (!link || (flags.linkRevocation && link.revokedAt)) return undefined;
  return getNote(link.noteId);
}

export function revokeLink(actor: string, id: string): SharedLink | undefined {
  const link = links.get(id);
  if (!link) return undefined;
  link.revokedAt = new Date().toISOString();
  record(actor, "link.revoke", id);
  return link;
}
TS
sed 's|createLink(req.user, req.body.noteId)|createLink(req.user, req.body.noteId, { expiresAt: req.body.expiresAt })|' "$DEST/src/sharing/routes.ts" > "$DEST/src/sharing/routes.ts.new" && mv "$DEST/src/sharing/routes.ts.new" "$DEST/src/sharing/routes.ts"
commit 2026-10-16T13:00:00Z "feat: store an expiry time on a shared link (P1)"
git tag line/p1-green

# PLANTED PL-SCOPE: a change outside the phase's allowedFiles
printf '\n// Mention links that expire this week in the digest text.\nexport function expiringLinksLine(count: number): string {\n  return `${count} links expire this week`;\n}\n' >> "$DEST/src/digest/digest.ts"
commit 2026-10-16T14:00:00Z "feat: a digest line for expiring links (P1)"
git tag line/p1-out-of-scope

# PLANTED PL-CANARY: a hidden scenario's canary phrase in a commit message
printf '\n// the boundary instant is refused\n' >> "$DEST/tests/expiry.test.ts"
commit 2026-10-16T15:00:00Z "test: cover the boundary case, see $CANARY"
git tag line/p1-canary

git checkout -q main
[ "$(git rev-parse main)" = "$BASE_HEAD" ] || { echo "build.sh: main moved" >&2; exit 1; }
git rev-parse main
