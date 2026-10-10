#!/usr/bin/env bash
# Tests for the human-facing repo files (readiness spec §4d, §6 W5, W6): the devcontainer, the issue forms,
# the PR template and the label scheme. YAML is parsed with Ruby's stdlib (present on the macOS and Ubuntu
# runners), JSONC with node after stripping comment lines.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
PASS=0
FAIL=0
ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
check() { if eval "$2"; then ok "$1"; else fail "$1"; fi; }

yaml_ok() { ruby -ryaml -e 'YAML.safe_load(File.read(ARGV[0]), permitted_classes: [], aliases: false)' "$1" 2>/dev/null; }
# Prints the value of a top-level YAML key as JSON.
yaml_get() { ruby -ryaml -rjson -e 'puts JSON.generate(YAML.safe_load(File.read(ARGV[0]))[ARGV[1]])' "$1" "$2"; }

echo "devcontainer"
DC=.devcontainer/devcontainer.json
DF=.devcontainer/Dockerfile
check "devcontainer.json exists" "[ -f $DC ]"
check "devcontainer.json parses (JSONC)" \
  "grep -vE '^[[:space:]]*//' $DC | node -e 'JSON.parse(require(\"fs\").readFileSync(0, \"utf8\"))'"
check "Dockerfile exists" "[ -f $DF ]"
sdk="$(node -e 'console.log(JSON.parse(require("fs").readFileSync("global.json","utf8")).sdk.version)')"
check "base image tag is the global.json SDK ($sdk)" "grep -qE '^FROM mcr\.microsoft\.com/dotnet/sdk:${sdk//./\\.}-[a-z]+@sha256:[0-9a-f]{64}\$' $DF"
check "every FROM and COPY --from image is pinned by digest" \
  "! grep -E '^(FROM|COPY --from=)' $DF | grep -vqE '@sha256:[0-9a-f]{64}'"
nvm="$(tr -d '[:space:]v' < .nvmrc)"
check "Node major matches .nvmrc ($nvm)" "grep -qE '^ARG NODE_VERSION=${nvm%%.*}\\.[0-9]+\\.[0-9]+\$' $DF"
check "Node tarball is checksum-verified" "grep -q 'sha256sum -c' $DF && grep -qE '^ARG NODE_SHA256_X64=[0-9a-f]{64}\$' $DF && grep -qE '^ARG NODE_SHA256_ARM64=[0-9a-f]{64}\$' $DF"
# Comment lines may name what's excluded; only the configuration itself counts.
check "no Docker-in-Docker, host socket, privileged mode or extra run args" \
  "! grep -hvE '^[[:space:]]*(#|//)' .devcontainer/* | grep -iE 'docker-in-docker|docker-outside-of-docker|docker\.sock|privileged|\"runArgs\"|\"mounts\"|\"features\"|--cap-add'"
check "the container runs as a non-root user" "grep -qE '^USER [a-z]+' $DF && grep -q '\"remoteUser\": \"ubuntu\"' $DC"
check "post-create script is executable and restores locked" \
  "[ -x .devcontainer/post-create.sh ] && grep -q 'dotnet restore AgenticWorkflow.slnx --locked-mode' .devcontainer/post-create.sh"
check "docs say it is for humans and never the agent sandbox" \
  "grep -qi 'never the agent sandbox' docs/devcontainer.md"

echo "issue forms"
for kind in bug feature skill-change idea-card; do
  f=".github/ISSUE_TEMPLATE/$kind.yml"
  check "$kind form exists and parses" "[ -f $f ] && yaml_ok $f"
  check "$kind form has name, description, labels and a body" \
    "[ \"\$(yaml_get $f name)\" != null ] && [ \"\$(yaml_get $f description)\" != null ] && [ \"\$(yaml_get $f labels)\" != null ] && [ \"\$(yaml_get $f body)\" != null ]"
  check "$kind form's labels all exist in .github/labels.yml" \
    "yaml_get $f labels | ruby -rjson -ryaml -e 'l=JSON.parse(STDIN.read); n=YAML.safe_load(File.read(\".github/labels.yml\")).map{|x| x[\"name\"]}; exit((l-n).empty? ? 0 : 1)'"
done
check "config.yml parses and turns blank issues off" \
  "yaml_ok .github/ISSUE_TEMPLATE/config.yml && [ \"\$(yaml_get .github/ISSUE_TEMPLATE/config.yml blank_issues_enabled)\" = false ]"
check "idea-card form carries the addendum §17.18 fields" \
  "for w in What Why Evidence 'Cost and risk' 'Eval plan' Dedupe; do grep -q \"label: \$w\" .github/ISSUE_TEMPLATE/idea-card.yml || exit 1; done"

echo "PR template"
PT=.github/pull_request_template.md
check "PR template exists" "[ -f $PT ]"
check "PR template has the pr-body sections in order" \
  "awk '/^## /{print}' $PT | tr '\n' '|' | grep -q '^## Summary|## Key decisions|## Evidence|## Tests|## Coverage|## Review resolution|'"

echo "labels"
LB=.github/labels.yml
check "labels.yml parses" "yaml_ok $LB"
check "every label is family:value with a 6-hex color and a description" \
  "ruby -ryaml -e 'YAML.safe_load(File.read(\"$LB\")).each{|l| exit 1 unless l[\"name\"] =~ /\\A(type|area|size|ring):[a-z0-9-]+\\z/ && l[\"color\"].to_s =~ /\\A[0-9a-f]{6}\\z/ && !l[\"description\"].to_s.empty? && l[\"description\"].length <= 100 }'"
check "no duplicate label names" "ruby -ryaml -e 'n=YAML.safe_load(File.read(\"$LB\")).map{|l| l[\"name\"]}; exit(n.uniq.size == n.size ? 0 : 1)'"
for fam in type area size ring; do
  check "the $fam: family is present" "grep -qE '^- name: \"?$fam:' $LB"
done
check "size: covers the size classes XS S M L XL" \
  "for s in xs s m l xl; do grep -qE \"^- name: \\\"?size:\$s\\\"?\$\" $LB || exit 1; done"
check "nothing applies the labels to the repo (no label-sync workflow)" \
  "! grep -rlE 'labels\\.yml|gh label (create|edit|delete|clone)' .github/workflows"

echo
echo "repo-templates: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
