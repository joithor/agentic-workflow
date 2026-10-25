#!/usr/bin/env bash
# Tests for scripts/ci/recipe-versions.sh — "a content change bumps the version" (setup-once spec §5.3). The sindri tool is a stub that
# hashes whole files (the real one hashes the normalized recipe), so what is tested is the script's flow: git objects not the working
# tree, the baseline manifest, one validate per HEAD recipe, and the exit codes. The rule itself is tested in Aw.Sindri.Tests.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$DIR/../ci/recipe-versions.sh"
fail=0
work="$(mktemp -d)"
cleanup() { rm -r -f "$work"; }
trap cleanup EXIT

# The stub: `recipe manifest <dir> [--json]` and `recipe validate <file> --against <manifest.json>`.
cat > "$work/fake-sindri" <<'STUB'
#!/usr/bin/env bash
entry() { # file -> id version hash
  local f="$1"
  printf '%s %s sha256:%s\n' "$(sed -n 's/^id: //p' "$f")" "$(sed -n 's/^version: //p' "$f")" "$(sha256sum "$f" | cut -d' ' -f1)"
}
[ "$1" = recipe ] || exit 2
case "$2" in
  manifest)
    dir="$3"
    if [ "${4:-}" = "--json" ]; then
      printf '{"recipes":['
      sep=""
      for f in "$dir"/*.yaml; do
        read -r id v h <<< "$(entry "$f")"
        printf '%s{"id":"%s","version":%s,"hash":"%s"}' "$sep" "$id" "$v" "$h"; sep=","
      done
      printf '],"invalid":[],"digest":"x"}\n'
    else
      for f in "$dir"/*.yaml; do read -r id v h <<< "$(entry "$f")"; echo "$id@$v -> $h"; done
    fi ;;
  validate)
    if [ -n "${AW_STUB_VALIDATE_RC:-}" ]; then echo "stub: the tool broke" >&2; exit "$AW_STUB_VALIDATE_RC"; fi
    read -r id v h <<< "$(entry "$3")"
    if grep -q "\"id\":\"$id\",\"version\":$v,\"hash\":\"" "$5" && ! grep -q "\"id\":\"$id\",\"version\":$v,\"hash\":\"$h\"" "$5"; then
      echo "RCP-VERSION-001: $id version $v is registered with another hash"; exit 1
    fi ;;
  *) exit 2 ;;
esac
STUB
chmod +x "$work/fake-sindri"

recipe() { printf 'schema: 1\nid: workflow.%s\nversion: %s\ndisplayName: %s\n' "$1" "$2" "$3"; }

repo="$work/repo"
mkdir -p "$repo/recipes/sindri"
git -C "$repo" init -q -b main
git -C "$repo" config user.email t@example.com
git -C "$repo" config user.name t
recipe a 1 "Alpha" > "$repo/recipes/sindri/workflow.a.yaml"
recipe b 1 "Beta" > "$repo/recipes/sindri/workflow.b.yaml"
git -C "$repo" add -A && git -C "$repo" commit -q -m base && git -C "$repo" tag base

run() { # name want-exit [extra args]  (runs in $repo with the stub)
  local name="$1" want="$2" got out
  shift 2
  out="$(cd "$repo" && AW_SINDRI_CMD="$work/fake-sindri" bash "$SCRIPT" "$@" 2>&1)" && got=0 || got=$?
  if [ "$got" != "$want" ]; then
    printf 'FAIL: %s (want exit %s, got %s)\n%s\n' "$name" "$want" "$got" "$out"; fail=1
  else
    echo "PASS: $name"
  fi
  LAST="$out"
}
expect_text() { case "$LAST" in *"$2"*) ;; *) printf 'FAIL: %s (output lacks %q)\n%s\n' "$1" "$2" "$LAST"; fail=1 ;; esac; }

run "unchanged recipes pass" 0 --base-ref base

recipe b 1 "Beta, reworded" > "$repo/recipes/sindri/workflow.b.yaml"
run "an edit that is not committed is not compared" 0 --base-ref base
git -C "$repo" commit -q -am "reword b"
run "a content change at the same version fails" 1 --base-ref base
expect_text "the failure names the file" "workflow.b.yaml"
expect_text "the failure shows the rule" "RCP-VERSION-001"

recipe b 2 "Beta, reworded" > "$repo/recipes/sindri/workflow.b.yaml"
git -C "$repo" commit -q -am "bump b"
run "a content change with a raised version passes" 0 --base-ref base

recipe c 1 "Gamma" > "$repo/recipes/sindri/workflow.c.yaml"
git -C "$repo" add -A && git -C "$repo" commit -q -m "new c"
run "a new recipe passes" 0 --base-ref base

git -C "$repo" tag empty "$(git -C "$repo" commit-tree -m empty "$(git -C "$repo" hash-object -t tree /dev/null)")"
run "a base with no recipes/sindri has nothing to compare" 0 --base-ref empty
expect_text "it says so" "nothing to compare"

AW_STUB_VALIDATE_RC=2 run "a validate tool error is exit 2, not a version failure" 2 --base-ref base
expect_text "its stderr is shown" "stub: the tool broke"

mkdir -p "$work/badgit"
printf '#!/usr/bin/env bash\n[ "$1" = archive ] && exit 1\nexec %s "$@"\n' "$(command -v git)" > "$work/badgit/git"
chmod +x "$work/badgit/git"
PATH="$work/badgit:$PATH" run "a git archive failure on the base ref fails the script" 2 --base-ref base
expect_text "it names the archive" "git archive base failed"

run "a base ref that is not a commit is a setup error" 2 --base-ref nope
run "an unknown argument is a usage error" 2 --verbose

exit "$fail"
