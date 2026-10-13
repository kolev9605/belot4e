# Decision: Hosting the Belote application

- **Slug**: deployment-options
- **Decided**: 2026-10-13
- **Verdict**: go
- **Provider**: Azure Container Apps (Consumption plan, one always-on replica, 0.25 vCPU / 0.5 GiB)
- **Artifacts reviewed**: intake.md, research.md, problem.md, concept.md

## Scorecard

| Criterion | Rating | Justification |
|-----------|--------|---------------|
| Problem validity | adequate | The app is local-only and cannot be shared; a real gap, though audience and demand beyond the owner are unconfirmed (problem.md, research.md "Users & Demand"). |
| Evidence strength | adequate | Repo facts (in-memory rooms, WebSockets-only SignalR, same-origin static hosting, no deploy config) are high confidence. Vendor prices and limits are medium/low confidence from search snippets, but enough to separate "always-on small instance" from "sleeps when idle". |
| Value vs. inaction | adequate | Inaction costs nothing but leaves multiplayer untested with real users; value is modest, cost is a few dollars a month. |
| Feasibility / appetite | strong | Options A and B are small (days); the server already serves the built client, so one deployable is credible. |
| Strategic fit | unknown | No project constitution consulted for hosting; the multiplayer/rooms feature (PR #9) implies sharing was intended. |
| Risk posture | adequate | Main risks are known: memory footprint on the smallest instance, price/terms drift, rooms lost on redeploy. They are cheap to validate and cheap to reverse; they are not yet mitigated. |

## Verdict & Rationale

**Go.** The problem is real if modest, evidence is sufficient to choose a direction, and a shaped concept exists with a small appetite. The cost of being wrong is low: the deployment is reversible and costs a few dollars a month at most. Strategic fit is `unknown` and is acknowledged rather than glossed. The recommended option (B, always-on small instance) rests on one open assumption, the budget ceiling, which decides between B and the $0 fallback A. That belongs in specification clarification and does not block the handoff. If the owner states a hard $0 requirement, take Option A instead and accept cold starts and lost idle rooms.

**Provider: Azure Container Apps.** The owner's rule was to pick Azure unless Fly.io is significantly cheaper. Estimated cost is about $4–5/mo on Azure (0.25 vCPU / 0.5 GiB, one replica, after the monthly free grant) against about $2/mo on Fly.io at 256 MB, or about $3–4 at 512 MB, which .NET probably needs. That gap of roughly $1–3 per month is not significant, so Azure wins. See `concept.md` for the estimate and the ordered fallbacks (free tier that sleeps, Azure App Service B1 at about $13/mo, Fly.io, VPS).

## If needs-clarification

Not applicable.

## If go — Handoff to `/speckit-specify`

- **Problem**: The Belote application runs only on a developer machine, so nobody else can play; the owner needs it reachable over the internet at the lowest practical cost and setup effort without making real-time play unreliable.
- **Chosen approach**: Option B on **Azure Container Apps** (Consumption plan, minimum and maximum replicas both 1, 0.25 vCPU / 0.5 GiB, about $4–5/mo), running the existing server as a container with the built web client served same-origin. Max replicas must stay 1 because rooms live in process memory.
- **Fallbacks, in order**: Option A (free tier that sleeps) if the budget must be $0; Azure App Service Basic B1 (about $13/mo) if the container route is troublesome; Fly.io or a VPS (Option C) only if Azure costs prove materially higher than estimated. Azure App Service Free (F1) is ruled out.
- **In scope / out of scope**:
  - In scope: a publicly reachable HTTPS URL, WebSocket support, a repeatable way to build and deploy the single deployable, basic configuration through environment variables.
  - Out of scope: horizontal scaling or HA, room persistence across redeploys, accounts or databases, game/UI changes, custom domain and deploy-on-merge pipeline unless pulled in during clarification, monitoring beyond host defaults.
- **Success metrics**:
  - Another person can open a link and join a room with no local setup.
  - Monthly cost stays at or below the agreed ceiling.
  - A full four-seat game completes without a host-caused disconnect or cold start.
  - Time from ready-to-deploy to a working public URL fits within a single session.
- **Carried-forward open questions**:
  - [NEEDS CLARIFICATION: Monthly budget ceiling; is $0 a hard requirement?]
  - [NEEDS CLARIFICATION: Expected audience size and concurrent rooms/players]
  - [NEEDS CLARIFICATION: Is losing in-progress rooms on idle/restart/redeploy acceptable?]
  - [NEEDS CLARIFICATION: Server memory footprint under a full table; does it fit the smallest instance?]
  - [NEEDS CLARIFICATION: Custom domain needed, or is a provider URL enough?]
  - [NEEDS CLARIFICATION: Manual deploy vs. automated deploy on merge to main]
  - [NEEDS CLARIFICATION: Confirm Azure Container Apps pricing in the Azure pricing calculator (idle memory rate is uncertain) before committing]
  - [NEEDS CLARIFICATION: Verify that an open SignalR WebSocket works with min = max = 1 replica and is not dropped on scale-in; test on deployment]
  - [NEEDS CLARIFICATION: Existing Azure subscription/account, and which region]
  - [NEEDS CLARIFICATION: Provider/account preferences or constraints]
