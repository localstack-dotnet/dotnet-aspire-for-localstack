# Security Policy

## Supported Versions

We maintain a versioning strategy aligned with .NET Aspire releases. Security patches are released based on the CVSS v3.0 Rating:

### Security Patch Policy

| CVSS v3.0 | v9.x (Current) |
| --------- | -------------- |
| 9.0-10.0  | ✅ All releases within previous 3 months |
| 4.0-8.9   | ✅ Most recent release |
| < 4.0     | ⚠️ Best effort |

## Reporting a Vulnerability

The .NET Aspire LocalStack integration team takes security bugs seriously. We appreciate your efforts to responsibly disclose your findings, and will make every effort to acknowledge your contributions.

**Security Infrastructure**: This repository has GitHub Advanced Security enabled with automated vulnerability detection, dependency scanning, code scanning, and secret detection to help maintain security standards.

To report a security vulnerability, please use one of the following methods:

### Preferred Method: GitHub Security Advisories

1. Go to the [Security tab](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/security) of this repository
2. Click "Report a vulnerability"
3. Fill out the security advisory form with details about the vulnerability

### Alternative Method: Email

Send an email to [localstack.dotnet@gmail.com](mailto:localstack.dotnet@gmail.com) with:

- A clear description of the vulnerability
- Steps to reproduce the issue
- Potential impact of the vulnerability
- Any suggested fixes (if available)

### Public Issues

For non-security related bugs, please use our [GitHub Issues](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues) tracker.

## Response Timeline

We will respond to security vulnerability reports within **48 hours** and will keep you informed throughout the process of fixing the vulnerability.

## Security Updates

Security updates will be released as soon as possible after a vulnerability is confirmed and a fix is available. We will:

1. Confirm the problem and determine the affected versions
2. Audit code to find any potential similar problems
3. Prepare fixes for all supported versions
4. Release new versions as quickly as possible

## Comments on this Policy

If you have suggestions on how this process could be improved, please submit a pull request or open an issue to discuss.
