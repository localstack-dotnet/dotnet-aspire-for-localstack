#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$1"
RESULTS_DIR="$2"
CONFIGURATION="${3:-Release}"

# 1️⃣  Get the multi-TFM list first …
TFM_RAW=$(dotnet msbuild "$PROJECT_PATH" \
          -getProperty:TargetFrameworks -nologo -v:q)

# 2️⃣  … fallback to single-TFM if empty
if [[ -z "$TFM_RAW" ]]; then
  TFM_RAW=$(dotnet msbuild "$PROJECT_PATH" \
            -getProperty:TargetFramework -nologo -v:q)
fi

if [[ -z "$TFM_RAW" ]]; then
  echo "Unable to determine target frameworks for $PROJECT_PATH" >&2
  exit 1
fi

# Normalise newlines → semicolons, then explode into an array
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
