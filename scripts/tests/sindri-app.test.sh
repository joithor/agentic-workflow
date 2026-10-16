#!/usr/bin/env bash
# Tests for scripts/sindri-app.sh on a sandboxed HOME and state dir, against a throwaway git repo holding a copy of the
# script, a stub UI publish (src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh), a stub scripts/binaries-lock.sh and a fake
# `dotnet` on PATH (its publish writes a stub `sindri` that prints its arguments), so nothing is built or fetched:
# the first run publishes both binaries into one build dir, links sindri-app, fetches temporal and runs sindri with the
# arguments; an unchanged tree reuses the build; an edit (committed or not) rebuilds; --rebuild forces one; two builds
# are kept; a failed publish leaves the last good build current; a foreign sindri-app on PATH is left alone, a dangling
# one replaced, AW_SINDRI_APP_NO_LINK=1 links nothing; stdout carries only sindri's output.
# Run: bash scripts/tests/sindri-app.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$WORK"' EXIT
unset AW_SINDRI_APP_NO_LINK AW_SINDRI_HOME CLAUDE_LOCAL_BIN DOTNET_ROOT
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state"
mkdir -p "$HOME"
CALLS="$WORK/calls"; : > "$CALLS"
export CALLS

# The fake dotnet: the pinned SDK is installed; publish -o DIR writes the sindri stub there (FAIL_PUBLISH=1 fails it).
mkdir -p "$WORK/fakebin"
cat > "$WORK/fakebin/dotnet" <<'EOF'
#!/usr/bin/env bash
case "$1" in
  --list-sdks) echo "10.0.401 [/fake/sdk]" ;;
  publish)
    echo "publish" >> "$CALLS"
    [ "${FAIL_PUBLISH:-0}" = 1 ] && { echo "error CS0001: boom"; exit 1; }
    out=""; while [ $# -gt 0 ]; do [ "$1" = -o ] && out="$2"; shift; done
    mkdir -p "$out"
    printf '#!/usr/bin/env bash\necho "sindri $*"\necho "root=$DOTNET_ROOT" >&2\n' > "$out/sindri"; chmod +x "$out/sindri" ;;
esac
EOF
chmod +x "$WORK/fakebin/dotnet"
export PATH="$WORK/fakebin:$PATH"

# The throwaway repo.
R="$WORK/repo"
mkdir -p "$R/scripts" "$R/src/Sindri/Aw.Sindri.Host" "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke"
cp "$DIR/sindri-app.sh" "$R/scripts/"
printf '{\n  "sdk": {\n    "version": "10.0.401"\n  }\n}\n' > "$R/global.json"
echo '<Project />' > "$R/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj"
printf '#!/usr/bin/env bash\necho ui >> "$CALLS"\nprintf "#!/bin/sh\\n" > "$1/sindri-ui"; chmod +x "$1/sindri-ui"\n' \
  > "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh"
printf '#!/usr/bin/env bash\necho "fetch $2" >> "$CALLS"\n' > "$R/scripts/binaries-lock.sh"
chmod +x "$R/scripts/binaries-lock.sh" "$R/src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh"
git -C "$R" init -q && git -C "$R" add -A && git -C "$R" -c user.email=t@t -c user.name=t commit -qm init

B="$AW_STATE_DIR/bin/sindri-app"
L="$HOME/.local/bin/sindri-app"
builds() { find "$B" -mindepth 1 -maxdepth 1 -type d ! -name '.*' | wc -l | tr -d ' '; }
publishes() { grep -c '^publish$' "$CALLS"; }

OUT="$(bash "$R/scripts/sindri-app.sh" status --json 2>"$WORK/err")"; RC=$?
check "first run: exit is sindri's" "$RC" "0"
check "first run: stdout is only sindri's, with the arguments" "$OUT" "sindri status --json"
check "first run: one host publish and one UI publish" "$(publishes):$(grep -c '^ui$' "$CALLS")" "1:1"
check "first run: temporal fetched" "$(grep -c '^fetch temporal$' "$CALLS")" "1"
S1="$(cat "$B/current")"
check "first run: both binaries in the current build" \
  "$(test -x "$B/$S1/sindri" && test -x "$B/$S1/sindri-ui" && echo yes)" "yes"
check "first run: DOTNET_ROOT defaults to ~/.dotnet" "$(grep -c "^root=$HOME/.dotnet$" "$WORK/err")" "1"
check "first run: sindri-app linked to the script" "$(readlink "$L")" "$R/scripts/sindri-app.sh"

OUT="$(bash "$L" 2>/dev/null)"
check "unchanged tree, through the link: no publish, bare sindri" "$(publishes):$OUT" "1:sindri "

echo '// edit' > "$R/src/Sindri/Aw.Sindri.Host/New.cs"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
S2="$(cat "$B/current")"
check "an uncommitted file rebuilds" "$(publishes):$([ "$S1" != "$S2" ] && echo new)" "2:new"
echo '// edit 2' >> "$R/src/Sindri/Aw.Sindri.Host/New.cs"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
check "the newest two builds are kept" "$(publishes):$(builds):$(test -d "$B/$S1" || echo pruned)" "3:2:pruned"

bash "$R/scripts/sindri-app.sh" --rebuild version >/dev/null 2>&1
check "--rebuild publishes the same stamp again" "$(publishes):$(builds)" "4:2"

S3="$(cat "$B/current")"
echo '// edit 3' >> "$R/src/Sindri/Aw.Sindri.Host/New.cs"
FAIL_PUBLISH=1 bash "$R/scripts/sindri-app.sh" >/dev/null 2>"$WORK/err"; RC=$?
check "a failed publish exits 1 with the log's tail" "$RC:$(grep -c 'CS0001' "$WORK/err")" "1:1"
check "a failed publish leaves the last good build current" "$(cat "$B/current"):$(test -d "$B/.lock" || echo unlocked)" "$S3:unlocked"

rm -f "$L"; echo '#!/bin/sh' > "$L"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>"$WORK/err"
check "a foreign sindri-app is left alone, with a note" "$(test -L "$L" || echo file):$(grep -c 'already something else' "$WORK/err")" "file:1"
rm -f "$L"; ln -s "$WORK/gone/sindri-app.sh" "$L"
bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
check "a dangling sindri-app is replaced" "$(readlink "$L")" "$R/scripts/sindri-app.sh"
rm -f "$L"
AW_SINDRI_APP_NO_LINK=1 bash "$R/scripts/sindri-app.sh" >/dev/null 2>&1
check "AW_SINDRI_APP_NO_LINK=1 links nothing" "$(test -e "$L" || test -L "$L" || echo none)" "none"

exit "$fail"
