<!-- Mirrors Lore SECURITY.md §"Reporting a vulnerability" as of 2026-05 -->
# Security policy

**Do not open a public GitHub Issue, Discussion, or pull request to report a
security vulnerability.** Public disclosure before a fix is available puts all
Lore users at risk.

## Reporting a vulnerability

Report through Epic Games' security channels:

- **Primary — Epic Games HackerOne program:** https://hackerone.com/epicgames
- **Alternative — email:** security@epicgames.com

Use the subject line "Lore C# SDK security" when reporting by email.

## Safe harbor

Epic doesn't pursue legal action against researchers acting in good faith under this policy.

## What to include

- A description of the vulnerability and how it can be exploited
- Step-by-step reproduction, or a minimal reproduction program
- The affected SDK version (run `dotnet list package` in your project directory)
- Your .NET version, OS, and CPU architecture
- Impact assessment — what can an attacker do?
- Your name and affiliation for credit (or note if you prefer anonymity)

## Scope

This file covers the `LoreVcs` NuGet package and its published platform
binaries. Vulnerabilities in the underlying Lore wire protocol, `lore-capi`, or
`loreserver` are also in scope — mention which component is affected.

## Response, disclosure, supported versions, and bug bounty

The full response timeline, embargo tracks, supported-version policy, CVE
coordination, and bug bounty terms are governed by the Lore project security
policy and apply uniformly to the SDK:

→ [Lore SECURITY.md](https://github.com/EpicGames/lore/blob/main/SECURITY.md)

If you do not receive an acknowledgement within 7 days, follow up at
security@epicgames.com with "Lore C# SDK security" in the subject line.
