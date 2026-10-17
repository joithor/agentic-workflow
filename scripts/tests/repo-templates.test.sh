#!/usr/bin/env bash
# Tests for the human-facing repo files (readiness spec §4d, §6 W5, W6): the devcontainer, the issue forms,
# the PR template and the label scheme. YAML is parsed with Ruby's stdlib (present on macOS and the Ubuntu CI
# runners) or, where Ruby is missing (a stock Ubuntu box), with python3's PyYAML (apt python3-yaml); JSONC with node
# after stripping comment lines.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
PASS=0
FAIL=0
ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
# The check runs in a subshell, so an `exit 1` inside it fails that check only and the suite goes on.
check() { if (eval "$2"); then ok "$1"; else fail "$1"; fi; }

if command -v ruby >/dev/null 2>&1; then
  yaml_ok() { ruby -ryaml -e 'YAML.safe_load(File.read(ARGV[0]), permitted_classes: [], aliases: false)' "$1" 2>/dev/null; }
  yaml_json() { ruby -ryaml -rjson -e 'puts JSON.generate(YAML.safe_load(File.read(ARGV[0])))' "$1"; }
  # Prints the value of a top-level YAML key as JSON.
  yaml_get() { ruby -ryaml -rjson -e 'puts JSON.generate(YAML.safe_load(File.read(ARGV[0]))[ARGV[1]])' "$1" "$2"; }
elif python3 -c 'import yaml' >/dev/null 2>&1; then
  yaml_ok() { python3 -I -c 'import sys, yaml; yaml.safe_load(open(sys.argv[1]))' "$1" 2>/dev/null; }
  yaml_json() { python3 -I -c 'import sys, json, yaml; print(json.dumps(yaml.safe_load(open(sys.argv[1]))))' "$1"; }
  yaml_get() { python3 -I -c 'import sys, json, yaml; print(json.dumps(yaml.safe_load(open(sys.argv[1]))[sys.argv[2]]))' "$1" "$2"; }
else
  echo "FAIL: no YAML parser. Install ruby (macOS has it; Debian/Ubuntu: sudo apt-get install -y ruby) or python3-yaml (sudo apt-get install -y python3-yaml)." >&2
  exit 1
fi

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
  "! grep -E '^(FROM|COPY --from=)' $DF | has -vE '@sha256:[0-9a-f]{64}'"
nvm="$(tr -d '[:space:]v' < .nvmrc)"
check "Node major matches .nvmrc ($nvm)" "grep -qE '^ARG NODE_VERSION=${nvm%%.*}\\.[0-9]+\\.[0-9]+\$' $DF"
check "Node tarball is checksum-verified" "grep -q 'sha256sum -c' $DF && grep -qE '^ARG NODE_SHA256_X64=[0-9a-f]{64}\$' $DF && grep -qE '^ARG NODE_SHA256_ARM64=[0-9a-f]{64}\$' $DF"
# Comment lines may name what's excluded; only the configuration itself counts. The pattern is kept in this
# variable so the probes below can prove it catches each way a devcontainer can gain privileges.
PRIV_RE='docker-in-docker|docker-outside-of-docker|docker\.sock|privileged|"runArgs"|"mounts"|"features"|"capAdd"|"securityOpt"|"dockerComposeFile"|"workspaceMount"|"initializeCommand"|"containerUser"|cap-add|cap_add|security_opt'
privilege_hits() { grep -hvE '^[[:space:]]*(#|//)' "$1"/* | grep -iE "$PRIV_RE"; }
check "no Docker-in-Docker, host socket, privileged mode, capabilities or extra run args" "! privilege_hits .devcontainer"
PROBE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/repo-templates-probe.XXXXXX")"
trap 'rm -rf "$PROBE_DIR"' EXIT
for snippet in '"runArgs": ["--privileged"]' '"capAdd": ["SYS_ADMIN"]' '"securityOpt": ["seccomp=unconfined"]' \
               '"dockerComposeFile": "compose.yml"' '"mounts": ["source=/var/run/docker.sock,target=/var/run/docker.sock,type=bind"]' \
               '"features": {"ghcr.io/devcontainers/features/docker-in-docker:2": {}}' 'cap_add: [SYS_ADMIN]' \
               '"workspaceMount": "source=/,target=/workspaces/host,type=bind"' '"initializeCommand": "touch /tmp/x"' \
               '"containerUser": "root"'; do
  printf '{\n  %s\n}\n' "$snippet" > "$PROBE_DIR/devcontainer.json"
  check "the privilege check catches: $snippet" "privilege_hits '$PROBE_DIR' > /dev/null"
done
# runs_as_ubuntu <Dockerfile> <devcontainer.json>: the image's last USER and the devcontainer's user are ubuntu.
runs_as_ubuntu() { [ "$(grep -E '^USER ' "$1" | tail -1)" = "USER ubuntu" ] && grep -q '"remoteUser": "ubuntu"' "$2"; }
check "the container runs as the non-root ubuntu user" "runs_as_ubuntu $DF $DC"
printf 'FROM x\nUSER ubuntu\nRUN true\nUSER root\n' > "$PROBE_DIR/Dockerfile.root"
printf '{\n  "remoteUser": "ubuntu"\n}\n' > "$PROBE_DIR/remote.json"
check "a Dockerfile that ends as root fails the user check" "! runs_as_ubuntu '$PROBE_DIR/Dockerfile.root' '$PROBE_DIR/remote.json'"
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
    "yaml_get $f labels | node -e 'const l=JSON.parse(require(\"fs\").readFileSync(0,\"utf8\")); const n=JSON.parse(process.argv[1]).map(x=>x.name); process.exit(l.every(x=>n.includes(x))?0:1)' \"\$(yaml_json .github/labels.yml)\""
done
check "config.yml parses and turns blank issues off" \
  "yaml_ok .github/ISSUE_TEMPLATE/config.yml && [ \"\$(yaml_get .github/ISSUE_TEMPLATE/config.yml blank_issues_enabled)\" = false ]"
check "idea-card form carries the addendum §17.18 fields" \
  "for w in What Why Evidence 'Cost and risk' 'Eval plan' Dedupe; do grep -q \"label: \$w\" .github/ISSUE_TEMPLATE/idea-card.yml || exit 1; done"

echo "PR template"
PT=.github/pull_request_template.md
check "PR template exists" "[ -f $PT ]"
check "PR template has the pr-body sections in order" \
  "awk '/^## /{print}' $PT | tr '\n' '|' | has '^## Summary|## Key decisions|## Evidence|## Tests|## Coverage|## Review resolution|'"

echo "labels"
LB=.github/labels.yml
check "labels.yml parses" "yaml_ok $LB"
check "every label is family:value with a 6-hex color and a description" \
  "yaml_json $LB | node -e 'const L=JSON.parse(require(\"fs\").readFileSync(0,\"utf8\")); process.exit(L.every(l=>/^(type|area|size|ring):[a-z0-9-]+\$/.test(l.name) && /^[0-9a-f]{6}\$/.test(String(l.color)) && String(l.description??\"\").length>0 && l.description.length<=100)?0:1)'"
check "no duplicate label names" "yaml_json $LB | node -e 'const n=JSON.parse(require(\"fs\").readFileSync(0,\"utf8\")).map(l=>l.name); process.exit(new Set(n).size===n.length?0:1)'"
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
