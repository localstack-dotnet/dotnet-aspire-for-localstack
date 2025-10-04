#!/usr/bin/env pwsh
# update-badge-win.ps1 - Post test results to BadgeSmith API (Windows)
# Usage: Called by action.yml on Windows runners

$ErrorActionPreference = "Continue" # Don't fail build on badge update failures

Write-Host "🔧 Running BadgeSmith update script for Windows..." -ForegroundColor Cyan

# Extract owner and repo from repository input
$repoParts = $env:INPUT_REPOSITORY -split '/'
$owner = $repoParts[0]
$repo = $repoParts[1]

# Normalize platform name
$platformLower = $env:INPUT_PLATFORM.ToLower()

# Extract branch from GitHub context
if ($env:GITHUB_EVENT_NAME -eq "pull_request") {
    $branch = $env:GITHUB_HEAD_REF
} else {
    $branch = $env:GITHUB_REF_NAME
}

# Calculate totals
$total = [int]$env:INPUT_TEST_PASSED + [int]$env:INPUT_TEST_FAILED + [int]$env:INPUT_TEST_SKIPPED

# Generate timestamp - ISO 8601 with milliseconds
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

# Generate UUID/NONCE
$nonce = [guid]::NewGuid().ToString("N")

# Create JSON payload for BadgeSmith API
$payload = @{
    platform = $env:INPUT_PLATFORM
    passed = [int]$env:INPUT_TEST_PASSED
    failed = [int]$env:INPUT_TEST_FAILED
    skipped = [int]$env:INPUT_TEST_SKIPPED
    total = $total
    url_html = $env:INPUT_TEST_URL_HTML
    timestamp = $timestamp
    commit = $env:INPUT_COMMIT_SHA
    run_id = $env:INPUT_RUN_ID
    workflow_run_url = "$($env:INPUT_SERVER_URL)/$($env:INPUT_REPOSITORY)/actions/runs/$($env:INPUT_RUN_ID)"
} | ConvertTo-Json -Compress

Write-Host "📊 Generated test results JSON for $($env:INPUT_PLATFORM):" -ForegroundColor Yellow
Write-Host $payload

# Compute HMAC-SHA256 signature
$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($env:INPUT_HMAC_SECRET)
$payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
$hashBytes = $hmac.ComputeHash($payloadBytes)
$hashHex = [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLower()
$signature = "sha256=$hashHex"

# Build BadgeSmith API URL
$apiUrl = "https://$($env:INPUT_API_DOMAIN)/tests/results/$platformLower/$owner/$repo/$branch"

Write-Host "🚀 Posting to BadgeSmith API: $apiUrl" -ForegroundColor Green
Write-Host "📅 Timestamp: $timestamp" -ForegroundColor Gray
Write-Host "🔑 Nonce: $nonce" -ForegroundColor Gray

# Send request to BadgeSmith API
try {
    $headers = @{
        "Content-Type" = "application/json"
        "X-Signature" = $signature
        "X-Timestamp" = $timestamp
        "X-Nonce" = $nonce
    }

    $response = Invoke-WebRequest `
        -Uri $apiUrl `
        -Method POST `
        -Headers $headers `
        -Body $payload `
        -UseBasicParsing

    $httpCode = $response.StatusCode
    $responseBody = $response.Content

    if ($httpCode -ge 200 -and $httpCode -lt 300) {
        Write-Host "✅ Successfully posted test results to BadgeSmith API (HTTP $httpCode)" -ForegroundColor Green
        Write-Host "Response:" -ForegroundColor Gray
        try {
            $responseBody | ConvertFrom-Json | ConvertTo-Json -Depth 10 | Write-Host
        } catch {
            Write-Host $responseBody
        }
    }
} catch {
    $httpCode = $_.Exception.Response.StatusCode.Value__
    $responseBody = ""

    try {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $responseBody = $reader.ReadToEnd()
        $reader.Close()
    } catch {
        $responseBody = $_.Exception.Message
    }

    Write-Host "⚠️ Failed to post test results to BadgeSmith API (HTTP $httpCode)" -ForegroundColor Yellow
    Write-Host "Response:" -ForegroundColor Gray
    try {
        $responseBody | ConvertFrom-Json | ConvertTo-Json -Depth 10 | Write-Host
    } catch {
        Write-Host $responseBody
    }
    # Don't fail the build for badge update failures
}

# Display badge URLs
Write-Host ""
Write-Host "🎯 BadgeSmith URLs for $($env:INPUT_PLATFORM):" -ForegroundColor Cyan
Write-Host ""
Write-Host "**$($env:INPUT_PLATFORM) Badge:**"
Write-Host "[![Test Results ($($env:INPUT_PLATFORM))](https://$($env:INPUT_API_DOMAIN)/badges/tests/$platformLower/$owner/$repo/$branch)](https://$($env:INPUT_API_DOMAIN)/redirect/test-results/$platformLower/$owner/$repo/$branch)"
Write-Host ""
Write-Host "**Raw URLs:**"
Write-Host "- Badge: https://$($env:INPUT_API_DOMAIN)/badges/tests/$platformLower/$owner/$repo/$branch"
Write-Host "- Redirect: https://$($env:INPUT_API_DOMAIN)/redirect/test-results/$platformLower/$owner/$repo/$branch"
Write-Host ""
Write-Host "**API Test:**"
Write-Host "curl `"https://$($env:INPUT_API_DOMAIN)/badges/tests/$platformLower/$owner/$repo/$branch`""
