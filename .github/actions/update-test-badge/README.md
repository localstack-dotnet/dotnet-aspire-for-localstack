# Update Test Results Badge Action

A reusable GitHub Action that posts test results to BadgeSmith API with HMAC authentication and displays badge URLs for README files.

## Purpose

This action simplifies the process of maintaining dynamic test result badges by:

- Creating structured JSON data from test results
- Posting the data to BadgeSmith API with HMAC-SHA256 authentication
- Supporting branch-specific badge URLs for master and feature branches
- Providing ready-to-use badge URLs for documentation

## Usage

```yaml
- name: Update test badge via BadgeSmith API
  if: always() && github.event_name == 'push' && github.ref == 'refs/heads/master'
  continue-on-error: true
  uses: ./.github/actions/update-test-badge
  with:
    platform: "Linux"
    test_passed: 1099
    test_failed: 0
    test_skipped: 0
    test_url_html: "https://github.com/owner/repo/runs/12345"
    commit_sha: ${{ github.sha }}
    run_id: ${{ github.run_id }}
    repository: ${{ github.repository }}
    server_url: ${{ github.server_url }}
    api_domain: "api.localstackfor.net"
    hmac_secret: ${{ secrets.TESTDATASECRET }}
```

## BadgeSmith API Structure

This action posts test results to the BadgeSmith API using RESTful endpoints:

**POST Endpoint:**

```
https://api.localstackfor.net/tests/results/{platform}/{owner}/{repo}/{branch}
```

**Badge Retrieval:**

```
https://api.localstackfor.net/badges/tests/{platform}/{owner}/{repo}/{branch}
```

**Example:**

- **Linux/master**: `https://api.localstackfor.net/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/master`
- **Windows/master**: `https://api.localstackfor.net/badges/tests/windows/localstack-dotnet/dotnet-aspire-for-localstack/master`

## Inputs

| Input | Description | Required | Default |
|-------|-------------|----------|---------|
| `platform` | Platform name (Linux, Windows, macOS) | ✅ | - |
| `test_passed` | Number of passed tests | ✅ | - |
| `test_failed` | Number of failed tests | ✅ | - |
| `test_skipped` | Number of skipped tests | ✅ | - |
| `test_url_html` | URL to test results page | ❌ | `''` |
| `commit_sha` | Git commit SHA | ✅ | - |
| `run_id` | GitHub Actions run ID | ✅ | - |
| `repository` | Repository in owner/repo format | ✅ | - |
| `server_url` | GitHub server URL | ✅ | - |
| `api_domain` | BadgeSmith API domain | ❌ | `api.localstackfor.net` |
| `hmac_secret` | HMAC secret for BadgeSmith authentication | ✅ | - |

## Outputs

This action produces:

- **API POST**: Posts test results to BadgeSmith API with HMAC authentication
- **Console Output**: Displays badge URLs ready for README usage
- **Debug Info**: Shows HTTP status codes and API responses
- **Branch Detection**: Automatically detects branch from GitHub context (master or feature branches)

## Generated JSON Format

The action creates JSON data in this format and posts it to BadgeSmith API:

```json
{
  "platform": "Linux",
  "passed": 1099,
  "failed": 0,
  "skipped": 0,
  "total": 1099,
  "url_html": "https://github.com/owner/repo/runs/12345",
  "timestamp": "2025-01-16T10:30:00Z",
  "commit": "abc123def456",
  "run_id": "12345678",
  "workflow_run_url": "https://github.com/owner/repo/actions/runs/12345678"
}
```

## HMAC Authentication

The action uses HMAC-SHA256 authentication with the following headers:

- **X-Signature**: `sha256=<hmac-sha256-hex-digest>`
- **X-Timestamp**: ISO 8601 timestamp with milliseconds
- **X-Nonce**: UUID v4 without hyphens (lowercase)

The signature is computed over the entire JSON payload using the provided `hmac_secret`.

## Error Handling

- **Non-essential**: Uses `continue-on-error: true` to prevent workflow failures
- **Graceful degradation**: Badge update failures don't fail the build (exit code not set on error)
- **HTTP status reporting**: Shows API response codes and full response bodies for debugging
- **Secure**: HMAC authentication prevents unauthorized badge updates

## Integration with BadgeSmith API

This action is designed to work with the BadgeSmith API that:

- Accepts test results via authenticated POST requests
- Stores data in DynamoDB with branch-specific keys
- Generates shields.io-compatible badge JSON dynamically
- Provides redirect endpoints to test result pages
- Supports multiple repositories, branches, and platforms

## Matrix Integration Example

```yaml
strategy:
  matrix:
    include:
      - os: ubuntu-22.04
        name: "Linux"
      - os: windows-latest
        name: "Windows"
      - os: macos-latest
        name: "macOS"

steps:
  - name: Update test badge via BadgeSmith API
    if: always() && github.event_name == 'push' && github.ref == 'refs/heads/master'
    continue-on-error: true
    uses: ./.github/actions/update-test-badge
    with:
      platform: ${{ matrix.name }}
      test_passed: '${{ steps.test-results.outputs.passed || 0 }}'
      test_failed: '${{ steps.test-results.outputs.failed || 0 }}'
      test_skipped: '${{ steps.test-results.outputs.skipped || 0 }}'
      test_url_html: ${{ steps.test-results.outputs.url_html || '' }}
      commit_sha: '${{ github.sha }}'
      run_id: '${{ github.run_id }}'
      repository: '${{ github.repository }}'
      server_url: '${{ github.server_url }}'
      api_domain: 'api.localstackfor.net'
      hmac_secret: '${{ secrets.TESTDATASECRET }}'
```

## Required Setup

1. **Deploy BadgeSmith API** to AWS (Lambda + API Gateway + DynamoDB)
2. **Generate HMAC Secret** for authentication
3. **Add to repository secrets** as `TESTDATASECRET`
4. **Configure workflow** to call action on master branch pushes

## Badge URLs Generated

The action displays ready-to-use markdown for README files:

```markdown
[![Linux Tests](https://img.shields.io/endpoint?url=https%3A%2F%2Fapi.localstackfor.net%2Fbadges%2Ftests%2Flinux%2Flocalstack-dotnet%2Fdotnet-aspire-for-localstack%2Fmaster)](https://api.localstackfor.net/redirect/test-results/linux?package=LocalStack.Aspire.Hosting)
```

## Advantages of BadgeSmith API Approach

- ✅ **Secure Authentication**: HMAC-SHA256 prevents unauthorized updates
- ✅ **RESTful Design**: Clean URL structure with branch/platform/repo hierarchy
- ✅ **Branch Support**: Separate badges for master and feature branches
- ✅ **Scalable**: DynamoDB backend handles high traffic
- ✅ **Dynamic**: Badges generated on-demand, no caching issues
- ✅ **No GitHub API Limits**: Independent infrastructure
- ✅ **Automatic Extraction**: Owner, repo, and branch extracted from GitHub context

## Troubleshooting

**Common Issues:**

- **401 Unauthorized**: Check `TESTDATASECRET` HMAC secret is correct
- **403 Forbidden**: Verify HMAC signature computation
- **404 Not Found**: Check API domain and endpoint structure
- **500 Server Error**: BadgeSmith API may be down or misconfigured

**Debug Steps:**

1. Check action output for HTTP status codes and response bodies
2. Verify `api_domain` is set to `api.localstackfor.net`
3. Confirm `hmac_secret` matches the BadgeSmith API configuration
4. Test badge URL manually: `curl "https://api.localstackfor.net/badges/tests/linux/localstack-dotnet/dotnet-aspire-for-localstack/master"`
5. Check GitHub Actions logs for HMAC signature and timestamp values

**Branch Detection:**

- **Pull Requests**: Uses `github.head_ref` (e.g., `feature/my-feature`)
- **Push to master**: Uses `github.ref_name` (e.g., `master`)
- Branch name is automatically included in API URL path
