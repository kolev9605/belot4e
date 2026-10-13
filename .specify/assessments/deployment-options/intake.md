# Idea Intake: Hosting options for the application

- **Slug**: deployment-options
- **Created**: 2026-10-13
- **Source**: pasted text (chat), relates to this repo (`src/Belote.Server`, `web/`)
- **Type**: cost-saving

## Idea (as captured)

> "what would be the easiest and cheapest option to host the application? What options do we have?"

## Restated

Identify the hosting options available for the Belote application (a .NET server under `src/Belote.Server` plus a web client under `web/`, with rooms/multiplayer shipped in the latest feature), and determine which is the easiest to set up and cheapest to run.

## Origin & Context

- **Raised by**: repo owner (via chat)
- **Trigger**: [NEEDS CLARIFICATION: first public deployment after the rooms/multiplayer feature (PR #9)? or moving off an existing setup?]

## First-Glance Unknowns

- [NEEDS CLARIFICATION: Is there any current hosting/deployment, or is this the first one?]
- [NEEDS CLARIFICATION: Expected audience and concurrency (friends/family vs. public)?]
- [NEEDS CLARIFICATION: Does multiplayer require persistent connections (WebSockets/long-lived), in-memory room state, or a single always-on instance?]
- [NEEDS CLARIFICATION: Is any persistent storage or database needed, or is state in-memory only?]
- [NEEDS CLARIFICATION: Budget ceiling (free tier acceptable? monthly cap?) and tolerance for cold starts/downtime?]
- [NEEDS CLARIFICATION: Is a custom domain / HTTPS required?]
- [NEEDS CLARIFICATION: Existing cloud accounts or provider preferences/constraints?]
- [NEEDS CLARIFICATION: Single deployable (server serves the web client) or separate static hosting for `web/`?]
- [NEEDS CLARIFICATION: Is CI/CD (e.g., GitHub Actions auto-deploy) wanted?]
