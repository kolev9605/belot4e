# Problem Definition: Hosting the Belote application

- **Slug**: deployment-options
- **Created**: 2026-10-13
- **Inputs used**: intake.md, research.md

## Problem Statement

The Belote application (a rooms-based multiplayer card game) runs only on a developer machine, so nobody else can play it. The owner needs it reachable by other players over the internet at the lowest practical cost and setup effort, without making real-time play unreliable.

## Affected Users & Stakeholders

- **Users**:
  - Players — can only play when they can reach a running, always-available table over a link; a dropped or sleeping host interrupts a game in progress (the table pauses within ~15 s of a lost connection, and rooms are held in memory only).
  - Room host (a player who creates and shares a room link) — needs the link to work whenever they want to play.
- **Stakeholders**:
  - Repo owner / maintainer — pays for hosting, carries setup and ongoing maintenance effort, decides what is acceptable.
  - [NEEDS CLARIFICATION: any other people who would fund, operate, or depend on the deployment]

## Goals

- Make the game playable by other people from a shareable URL.
- Keep the recurring cost as low as possible, with a preference for near-zero.
- Keep initial setup and ongoing upkeep small enough for one person to manage.
- Keep real-time play reliable enough for a full game to complete without avoidable disconnects.

## Non-Goals

- Horizontal scaling or high availability (the current design holds room state in one process's memory).
- Persistent storage or accounts beyond what the application already does.
- Choosing a specific provider or designing the deployment pipeline (that is the shaping stage).
- Changes to game rules, bots, or UI.

## Success Metrics

- Another person can open a link and join a room without any local setup (qualitative; baseline: not possible today).
- Monthly hosting cost is at or below an agreed ceiling (baseline: $0 today because nothing is hosted; ceiling [NEEDS CLARIFICATION]).
- A full game (four seats, one hand through to completion) finishes without a host-caused disconnect or cold start (baseline: unknown, untested).
- Time from "ready to deploy" to a working public URL is within a single session (qualitative; baseline: no deployment configuration exists).

## Cost of Inaction

The game stays a local-only project: it cannot be played with friends or family, and no real-world feedback on the multiplayer feature (rooms, reconnect, bot takeover) can be gathered. There is no financial cost, and no demand evidence exists yet that anyone beyond the owner wants to play.

## Open Questions

- [NEEDS CLARIFICATION: Expected audience size and concurrent rooms/players]
- [NEEDS CLARIFICATION: Monthly budget ceiling; is $0 a hard requirement or a preference?]
- [NEEDS CLARIFICATION: Is losing in-progress rooms on idle/restart/redeploy acceptable?]
- [NEEDS CLARIFICATION: Is a custom domain required, or is a provider-assigned HTTPS URL enough?]
- [NEEDS CLARIFICATION: Is automated deployment (e.g., on merge to main) wanted, or is manual deploy fine?]
- [NEEDS CLARIFICATION: Server memory footprint under load (needed to judge small free/cheap instance sizes); not measured]
- [NEEDS CLARIFICATION: Official current limits for the candidate free tiers (Azure App Service F1 WebSocket cap, Fly.io allowance, Hetzner post-June-2026 prices, Container Apps open-WebSocket billing)]
- [NEEDS CLARIFICATION: Existing cloud accounts or provider preferences/constraints]
