#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$1"
RESULTS_DIR="$2"
CONFIGURATION="${3:-Release}"

# Same property trick as in PowerShell
TFM_RAW=$(dotnet msbuild "$PROJECT_PATH" \
         -getProperty:TargetFrameworks,TargetFramework -nologo -v:q)

if [[ -z "$TFM_RAW" ]]; then
  echo "Unable to determine target frameworks for $PROJECT_PATH" >&2
  exit 1
fi

# Collapse newlines → semicolons → array
IFS=';' read -ra TFMS <<< "$(echo "$TFM_RAW" | tr -d '\r\n')"

echo "📋 Target frameworks: ${TFMS[*]}"

for tfm in "${TFMS[@]}"; do
  tfm="$(echo "$tfm" | xargs)"   # trim
  [[ -z "$tfm" ]] && continue

  echo "🧪 $tfm ..."
  dotnet test "$PROJECT_PATH" -c "$CONFIGURATION" -f "$tfm" --no-build \
         --logger "trx;LogFileName=testResults-$tfm.trx" \
         --results-directory "$RESULTS_DIR"
done
