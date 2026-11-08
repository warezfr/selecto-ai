# Security policy

## Supported versions

Only the latest release receives security fixes.

## Reporting a vulnerability

Please do not open a public issue for a security problem. Send a private report to **contact@span.observer** with:

- the Selecto AI version and edition (installer, portable or Microsoft Store), and your Windows version;
- a description of the problem and its impact;
- steps to reproduce, or a proof of concept.

We aim to acknowledge your report within 5 working days and to give you an assessment or a fix plan within 30 days. These are indicative delays, not a guarantee: this is a small, free project. We will credit you in the release notes if you wish.

## Scope

In scope: the Selecto AI application and its installers, how it stores API keys, how it reads the selection and the clipboard, the extension sandbox, and the update check.

Out of scope: vulnerabilities in third-party services (translation, dictionary and AI providers), in Windows itself, or in extensions you installed from other sources.

## Verifying downloads

Installers on GitHub are not code-signed. Each release includes `SHA256SUMS.txt`: compare it with the hash of your download (`Get-FileHash <file> -Algorithm SHA256`).
