# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| Latest  | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in this project, please report it responsibly.

**Do not open a public GitHub issue for security vulnerabilities.**

Instead, please use one of these methods:

1. **GitHub Security Advisories** — Use the [Security tab](https://github.com/sauravbhattacharya001/prompt/security/advisories) to privately report a vulnerability.
2. **Email** — Contact the maintainer directly.

### What to Include

- A description of the vulnerability
- Steps to reproduce the issue
- Potential impact
- Suggested fix (if any)

### Response Timeline

- **Acknowledgment:** Within 48 hours
- **Initial assessment:** Within 1 week
- **Fix or mitigation:** Dependent on severity

### Severity Levels

| Severity | Description | Target Response |
|----------|-------------|-----------------|
| Critical | Remote code execution, credential exposure | 24–48 hours |
| High     | Injection attacks, data leakage | 1 week |
| Medium   | Denial of service, information disclosure | 2 weeks |
| Low      | Minor issues, hardening suggestions | Next release |

## Security Measures in This Library

This library includes several built-in security features:

- **Prompt injection detection** — `PromptGuard` detects 10+ attack vectors including role hijacking, delimiter injection, and encoding attacks
- **Input sanitization** — Automatic sanitization of prompt inputs to prevent injection
- **Payload size limits** — `SerializationGuards` enforces a 10 MB maximum on JSON payloads to prevent denial-of-service
- **Credential protection** — API keys and endpoint URIs are excluded from JSON serialization (`[JsonIgnore]`) to prevent accidental leakage in logs or exports
- **Safe deserialization** — All JSON deserialization uses validated, size-checked inputs

## Best Practices for Users

- **Never hardcode API keys** — Use environment variables or Azure Key Vault
- **Enable content filtering** — Use `PromptGuard` to validate inputs before sending to the API
- **Set token budgets** — Use `TokenBudget` to prevent runaway costs
- **Monitor rate limits** — Use `PromptRateLimiter` to stay within API limits
- **Review prompt templates** — Audit templates for potential injection vectors

## Dependencies

This project uses GitHub's Dependabot for automated dependency updates and CodeQL for static analysis. Security patches for dependencies are prioritized.
