# Concept: Hosting the Belote application

- **Slug**: deployment-options
- **Created**: 2026-10-13
- **Recommended option**: B — Single always-on container on **Azure Container Apps** (Consumption plan, minimum replicas = 1)
- **Provider decision (2026-10-13)**: Azure is the default. Fly.io is chosen only if it is *significantly* cheaper; the estimate shows it is not (see Recommendation).

## Options

### Option A — Free-tier host that sleeps when idle (Render Free / Azure Container Apps)
- **Sketch**: Package the server (which already serves the built web client) as one deployable and put it on a $0 plan that stops the process when nobody is connected. Players open a link; the first visitor waits for a cold start, then play proceeds as normal.
- **Appetite**: small
- **Trade-offs**: Wins on cost ($0) and on zero idle spend. Sacrifices: ~1 minute cold start for the first visitor, and because rooms live in memory, any idle spin-down or redeploy erases rooms and in-progress games. Render counts WebSocket messages as activity, so an active game should stay up, but a lobby left idle can vanish. Container Apps' treatment of quiet open WebSockets is unconfirmed.
- **Rabbit holes**: Free-tier terms changing again; the host reclaiming a room mid-game; needing reconnect/rejoin behaviour to tolerate a cold-started server (rejoin tokens are held in memory, so a restart invalidates them).

### Option B — Single always-on small instance on Azure Container Apps (about $4–5/mo)
- **Sketch**: Same single deployable, packaged as a container and run continuously as one replica (minimum replicas = 1, maximum = 1) on the Azure Container Apps Consumption plan, sized at the smallest 0.25 vCPU / 0.5 GiB. The platform provides HTTPS and WebSocket support. Players use a stable URL that is always up; rooms survive as long as the process does.
- **Appetite**: small
- **Trade-offs**: Wins on reliability and simplicity: no cold starts, no idle spin-down, WebSockets work, minimal operations, and it stays inside the Azure ecosystem for the .NET stack and any future Azure services. The monthly free grant (180,000 vCPU-s, 360,000 GiB-s, 2M requests) offsets part of the always-on cost. Sacrifices: a small recurring bill of roughly $4–5/mo (see estimate under Recommendation), slightly more than Fly.io. Redeploys still drop rooms because state is in memory.
- **Rabbit holes**: A container image and registry are needed (the repo has none today); whether an open-but-quiet WebSocket counts as active (affects only the idle vs. active rate); 0.5 GiB may be tight for .NET 10 under load (footprint not measured), which raises the cost; idle memory rate is not confirmed from an official source.
- **Note**: Pin max replicas to 1; room state is in process memory, so a second replica would split rooms across instances.

### Option B′ — Same shape on Fly.io (about $2–4/mo) — alternative, not chosen
- **Sketch**: The same always-on single instance on Fly.io (shared-cpu-1x).
- **Appetite**: small
- **Trade-offs**: 256 MB listed at about $1.94–2.02/mo (unconfirmed); likely needs 512 MB for .NET, which narrows the gap with Azure. No free allowance confirmed. A second cloud account and tooling to learn.
- **Rabbit holes**: Same memory-footprint question; regional price markups; terms drift.

### Option C — Self-managed VPS (Hetzner, about €4–6/mo)
- **Sketch**: Rent a small Linux server, install the runtime or a container engine, put a TLS-terminating reverse proxy in front, and run the server as a service. Same always-on behaviour as Option B with full control.
- **Appetite**: medium
- **Trade-offs**: Wins on predictable cost and headroom (more RAM than the smallest PaaS sizes) and no platform lock-in. Sacrifices: the most hands-on setup and upkeep (OS patching, certificates, firewall, restarts, backups), and prices rose twice in 2026.
- **Rabbit holes**: TLS/domain setup, update and monitoring burden falling on one person, security hardening of an internet-facing box.

### (Not shaped) Do nothing
- Remains valid: the game stays local-only at zero cost and zero effort; see Cost of Inaction in `problem.md`.

## Recommendation

Proceed with **Option B on Azure Container Apps**. It meets the goals that matter most (shareable URL, reliable real-time play, low upkeep) at a cost of a few dollars a month, and it avoids the gameplay failure modes the research found in the $0 tiers (cold starts, idle spin-down, WebSocket caps, instance reclaim).

**Why Azure and not Fly.io.** The owner's rule: pick Azure unless Fly.io is significantly cheaper. Rough monthly estimate for one always-on replica (30 days = 2,592,000 s), using third-party-cited East US rates (idle vCPU $0.000003/s, memory $0.000003/GiB-s, active vCPU $0.000024/s) and the free grant:

| | Azure Container Apps (0.25 vCPU, 0.5 GiB, min 1) | Fly.io (shared-cpu-1x) |
|---|---|---|
| Compute/memory | vCPU: (648,000 − 180,000 free) × $0.000003 ≈ $1.40; memory: (1,296,000 − 360,000 free) × $0.000003 ≈ $2.81 | 256 MB ≈ $1.94–2.02; 512 MB likely a few dollars (not verified) |
| Active play time | about +$0.5/mo for ~20 h of active play | included in the instance price |
| **Total** | **≈ $4–5/mo** | **≈ $2/mo at 256 MB; about $3–4 if 512 MB is needed** |

The difference is roughly $1–3 per month, and Fly's 256 MB figure probably will not fit .NET, so it is not significantly cheaper. No strong argument overrides the Azure default, so **Azure Container Apps is the provider.**

**Fallbacks**, in order:
1. Option A (free tier that sleeps) if the budget must be exactly $0 and losing idle rooms is acceptable.
2. Azure App Service Basic B1 (about $13.14/mo, 1 core / 1.75 GB, Always On and WebSockets supported) if the container route proves troublesome. It is simpler but about three times the price.
3. Fly.io (Option B′) or a VPS (Option C) only if Azure costs prove materially higher than estimated.

Azure App Service Free (F1) is ruled out: reported 5-WebSocket cap and no Always On.

Because the audience, budget ceiling and memory footprint are unknown, the decision stage should confirm that a few dollars a month is acceptable before committing; if a hard $0 is required, Option A becomes the recommendation.

## Out of Scope (for the recommended option)

- Horizontal scaling, high availability, or any change that survives redeploys (room persistence)
- Accounts, databases, or new persistent storage
- Custom domain and automated deploy-on-merge pipeline (unless the decision stage pulls them in)
- Changes to game rules, bots, or UI
- Monitoring/alerting beyond what the host provides

## Assumptions to Validate

- A few dollars per month is acceptable to the owner (budget ceiling is unconfirmed).
- The server fits within the smallest instance size (memory footprint under a full table not measured).
- Azure Container Apps costs roughly $4–5/mo for one always-on 0.25 vCPU / 0.5 GiB replica; rates come from a third-party blog citing the Azure Retail Prices API, and the idle memory rate differs between price lists. Confirm in the Azure pricing calculator.
- Azure Container Apps supports WebSockets/SignalR on a single replica with minimum replicas = 1, and an open WebSocket does not cause scale-in (docs did not confirm; test it).
- Fly.io would not be significantly cheaper once a realistic memory size (probably 512 MB) is used.
- Losing rooms on redeploy or restart is acceptable (no persistence is planned).
- Expected audience is small (friends/family), so a single instance is sufficient.
