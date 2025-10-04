#!/bin/bash
set -euo pipefail

# update-badge-unix.sh - Post test results to BadgeSmith API (Linux/macOS)
# Usage: Called by action.yml on Linux and macOS runners

echo "🔧 Running BadgeSmith update script for Unix/macOS..."

# Extract owner and repo from repository input
IFS='/' read -ra REPO_PARTS <<< "${INPUT_REPOSITORY}"
OWNER="${REPO_PARTS[0]}"
REPO="${REPO_PARTS[1]}"

# Normalize platform name
PLATFORM_LOWER=$(echo "${INPUT_PLATFORM}" | tr '[:upper:]' '[:lower:]')

# Extract branch from GitHub context
if [[ "${GITHUB_EVENT_NAME}" == "pull_request" ]]; then
  BRANCH="${GITHUB_HEAD_REF}"
else
  BRANCH="${GITHUB_REF_NAME}"
fi

# Calculate totals
TOTAL=$((INPUT_TEST_PASSED + INPUT_TEST_FAILED + INPUT_TEST_SKIPPED))

# Generate timestamp - macOS compatible (no milliseconds)
if [[ "$OSTYPE" == "darwin"* ]]; then
  # macOS: use gdate if available, otherwise fall back to seconds precision
  if command -v gdate &> /dev/null; then
    TIMESTAMP=$(gdate -u +"%Y-%m-%dT%H:%M:%S.%3NZ")
  else
    TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  fi
else
  # Linux: GNU date supports %N
  TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.%3NZ")
fi

# Generate UUID - compatible with both Linux and macOS
if command -v uuidgen &> /dev/null; then
  NONCE=$(uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]')
else
  # Fallback: generate from /dev/urandom
  NONCE=$(cat /dev/urandom | LC_ALL=C tr -dc 'a-f0-9' | head -c 32)
fi

# Create JSON payload for BadgeSmith API
cat > test-results.json << EOF
{
  "platform": "${INPUT_PLATFORM}",
  "passed": ${INPUT_TEST_PASSED},
  "failed": ${INPUT_TEST_FAILED},
  "skipped": ${INPUT_TEST_SKIPPED},
  "total": ${TOTAL},
  "url_html": "${INPUT_TEST_URL_HTML}",
  "timestamp": "${TIMESTAMP}",
  "commit": "${INPUT_COMMIT_SHA}",
  "run_id": "${INPUT_RUN_ID}",
  "workflow_run_url": "${INPUT_SERVER_URL}/${INPUT_REPOSITORY}/actions/runs/${INPUT_RUN_ID}"
}
EOF

echo "📊 Generated test results JSON for ${INPUT_PLATFORM}:"
cat test-results.json | jq '.' 2>/dev/null || cat test-results.json

# Prepare HMAC authentication
PAYLOAD_JSON=$(cat test-results.json)

# Compute HMAC-SHA256 signature
SIGNATURE="sha256=$(echo -n "$PAYLOAD_JSON" | openssl dgst -sha256 -hmac "${INPUT_HMAC_SECRET}" -binary | xxd -p -c 256)"

# Build BadgeSmith API URL
API_URL="https://${INPUT_API_DOMAIN}/tests/results/${PLATFORM_LOWER}/${OWNER}/${REPO}/${BRANCH}"

echo "🚀 Posting to BadgeSmith API: ${API_URL}"
echo "📅 Timestamp: ${TIMESTAMP}"
echo "🔑 Nonce: ${NONCE}"

# Send request to BadgeSmith API
HTTP_CODE=$(curl -s -w "%{http_code}" -o response.tmp \
  -X POST "${API_URL}" \
  -H "Content-Type: application/json" \
  -H "X-Signature: ${SIGNATURE}" \
  -H "X-Timestamp: ${TIMESTAMP}" \
  -H "X-Nonce: ${NONCE}" \
  -d "$PAYLOAD_JSON")

RESPONSE_BODY=$(cat response.tmp)
rm -f response.tmp

if [[ "$HTTP_CODE" -ge 200 && "$HTTP_CODE" -lt 300 ]]; then
  echo "✅ Successfully posted test results to BadgeSmith API (HTTP $HTTP_CODE)"
  echo "Response:"
  echo "$RESPONSE_BODY" | jq . 2>/dev/null || echo "$RESPONSE_BODY"
else
  echo "⚠️ Failed to post test results to BadgeSmith API (HTTP $HTTP_CODE)"
  echo "Response:"
  echo "$RESPONSE_BODY" | jq . 2>/dev/null || echo "$RESPONSE_BODY"
  # Don't fail the build for badge update failures
fi

# Display badge URLs
echo ""
echo "🎯 BadgeSmith URLs for ${INPUT_PLATFORM}:"
echo ""
echo "**${INPUT_PLATFORM} Badge:**"
echo "[![Test Results (${INPUT_PLATFORM})](https://${INPUT_API_DOMAIN}/badges/tests/${PLATFORM_LOWER}/${OWNER}/${REPO}/${BRANCH})](https://${INPUT_API_DOMAIN}/redirect/test-results/${PLATFORM_LOWER}/${OWNER}/${REPO}/${BRANCH})"
echo ""
echo "**Raw URLs:**"
echo "- Badge: https://${INPUT_API_DOMAIN}/badges/tests/${PLATFORM_LOWER}/${OWNER}/${REPO}/${BRANCH}"
echo "- Redirect: https://${INPUT_API_DOMAIN}/redirect/test-results/${PLATFORM_LOWER}/${OWNER}/${REPO}/${BRANCH}"
echo ""
echo "**API Test:**"
echo "curl \"https://${INPUT_API_DOMAIN}/badges/tests/${PLATFORM_LOWER}/${OWNER}/${REPO}/${BRANCH}\""
