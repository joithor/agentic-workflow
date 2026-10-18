# shellcheck shell=bash
# Sourced by src/Sindri/aot-smoke/run.sh and scripts/sindri-app.sh: the git pathspecs of every file the Sindri host
# publish reads, so the two scripts copy one list and cannot drift apart (a file outside src/ the build embeds, such as
# config/launcher/builtins.json, or an AdditionalFiles banned-symbols list, is added here once).
# Use: git -C "$root" ls-files -z --cached --others --exclude-standard -- "${SINDRI_BUILD_PATHSPECS[@]}"
SINDRI_BUILD_PATHSPECS=(
  global.json nuget.config .editorconfig 'Directory.*' 'BannedSymbols*.txt' CodeMetricsConfig.txt context/onyx.lock config/launcher
  src/Aw.Kernel src/Aw.Kernel.Data src/Aw.Kernel.Telemetry src/Redaction src/Aw.Analyzers src/Context src/Judge src/Sindri
)
