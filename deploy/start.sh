#!/usr/bin/env bash
# deploy/start.sh — the image's entry (installed as /opt/sindri/start.sh, run under tini). It seeds the volume once and then
# runs `sindri serve` in the foreground as this process (exec), so SIGTERM reaches serve, which stops its Temporal dev server.
#
# Sindri looks for the temporal CLI only at $AW_STATE_DIR/cache/bin/temporal-<version>/temporal, with no override, and the
# image's root is read-only, so the binary baked into /opt/sindri/seed is copied there when the volume does not hold it yet.
# The destination is versioned (temporal-<version>), so a new volume gets the binary and so does a new temporal pin after an upgrade;
# a restart on a volume that already holds this version copies nothing and replaces nothing.
# The environment (AW_STATE_DIR, HOME, ...) is the image's ENV, not set here: a `kubectl exec` doesn't run this script.
set -euo pipefail

: "${AW_STATE_DIR:?AW_STATE_DIR must be set (the image sets /data/state)}"
SEED=/opt/sindri/seed
SINDRI=/opt/sindri/sindri

umask 077
mkdir -p "$AW_STATE_DIR" "${HOME:-/data/home}" "${AW_SINDRI_HOME:-$AW_STATE_DIR/sindri-app}"

for dir in "$SEED"/cache/bin/*/; do
  [ -d "$dir" ] || continue
  dest="$AW_STATE_DIR/cache/bin/$(basename "$dir")"
  if [ ! -x "$dest/temporal" ]; then
    # Copied beside the destination and moved in, so a kill mid-copy never leaves a half binary that passes the check above.
    tmp="$dest.tmp.$$"
    mkdir -p "$tmp"
    cp -a "$dir". "$tmp/"
    mv -T "$tmp" "$dest" 2>/dev/null || { cp -a "$tmp/." "$dest/" && rm -r "$tmp"; }
  fi
done

exec "$SINDRI" serve "$@"
