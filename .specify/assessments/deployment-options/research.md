# Idea Research: Hosting options for the application

- **Slug**: deployment-options
- **Created**: 2026-10-13
- **Evidence confidence (overall)**: medium — repo facts are high confidence (read directly); vendor prices/limits come from web-search snippets and third-party summaries, not the vendors' official pages, and several are volatile in 2026. Re-verify any number before committing.

## Users & Demand

- No user/demand signal exists in the repo or intake; audience size and concurrency are unknown — [ASSUMPTION: small group of friends/family playing private rooms] (confidence: low)

## Prior Art

- No existing deployment config in the repo: no Dockerfile or deploy workflow; `.github/workflows/ci.yml` only builds/tests (jobs `build-and-test`, `web`) — [source: repo, `.github/workflows/ci.yml`] (confidence: high)
- The server can already serve the built web client same-origin via the `Web:RootPath` setting (`MapFallbackToFile("index.html")`), so a single deployable process is supported — [source: `src/Belote.Server/ServerHost.cs`] (confidence: high)
- The web client connects to the hub at relative `/table` using SignalR with WebSockets only (`HttpTransportType.WebSockets`, negotiation still on) — [source: `web/src/connection/hubClient.ts`] (confidence: high)

## Market & Context

Options found (prices are indicative, secondary sources):

| Option | Cost (indicative) | WebSocket/always-on fit | Notes |
|---|---|---|---|
| Azure App Service Free (F1) | $0 | Poor: reported 5 concurrent WebSocket connections cap (HTTP 429 beyond), no Always On, 32-bit only | Fits a .NET stack but a 4-player table plus spectators/reconnects would hit the cap quickly — [Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/1358483/cant-connect-to-free-webapp-on-azure-through-webso), [agentdeals.dev](https://agentdeals.dev/azure-free-tier-2026) (medium) |
| Render Free web service | $0 | Works, but spins down after 15 min without HTTP request or incoming WebSocket message; ~1 min cold start; 750 free hrs/month; no persistent disk | Since Feb 2026 WebSocket messages count as activity — [Render changelog](https://render.com/changelog/free-web-services-now-remain-active-while-receiving-websocket-messages), [Render docs](https://render.com/docs/free) (medium) |
| Azure Container Apps (Consumption) | $0 within grant (180,000 vCPU-s, 360,000 GiB-s, 2M requests per subscription/month) | Scale-to-zero default; unconfirmed how idle open WebSockets count as "active" | Needs Docker image; in-memory rooms are lost on scale-to-zero — [Azure pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/) (medium) |
| Fly.io | ~$1.94–2.02/mo for shared-cpu-1x 256MB (Ashburn); pay-as-you-go, free allowance unconfirmed | Good: real always-on VM, WebSockets supported | 256MB may be tight for .NET 10; memory footprint not measured — [Fly pricing](https://fly.io/docs/pricing/) (medium) |
| Hetzner Cloud VPS (CX23 / CAX11) | ~€4–6/mo incl. IPv4 (raised in April and June 2026; exact current price unverified) | Good: full control, always-on | Most manual setup (OS, TLS via reverse proxy, updates) — [agentdeals.dev](https://agentdeals.dev/hetzner-pricing-2026), [privatedevops.com](https://privatedevops.com/news/hetzner-june-2026-cloud-price-increase-what-to-do) (low–medium) |
| Oracle Cloud Always Free (Ampere A1 VM) | $0 | Good if it keeps running | Idle instances may be reclaimed (7-day 95th-percentile CPU/network/memory below a threshold, reported 10–20%); blogs report an allowance halving in June 2026, unconfirmed — [51sec.org](https://blog.51sec.org/2023/02/oracle-cloud-cleaning-up-idle-compute.html), [braindetox.kr](https://braindetox.kr/en/posts/oracle_always_free_tier_reduced_2026.html) (low) |
| Self-host at home (PC / Raspberry Pi + tunnel) | Electricity only | Good | Not researched here; uptime and exposure depend on home network — [ASSUMPTION] (low) |

### Azure vs. Fly.io follow-up (2026-10-13)

- Azure Container Apps Consumption plan with minimum replicas = 1 stops scale-to-zero; an inactive replica at its minimum is billed at a reduced idle rate, and an active replica (above 0.01 vCPU or 1,000 bytes/s received) at the higher active rate — [source: Azure pricing page via search snippet](https://azure.microsoft.com/en-us/pricing/details/container-apps/) (medium)
- Rates quoted by a third party citing the Azure Retail Prices API (East US): vCPU active $0.000024/s, vCPU idle $0.000003/s, memory $0.000003/GiB-s, requests $0.40/M; the Azure China price list suggests idle memory may not be discounted in every list — [source: devzero.io blog via search snippet](https://www.devzero.io/blog/azure-container-apps-pricing) (low–medium)
- Estimate for one always-on 0.25 vCPU / 0.5 GiB replica for 30 days after the free grant: about $4–5/mo, derived arithmetic, not a vendor quote — [ASSUMPTION based on the figures above] (low–medium)
- Azure App Service Basic B1 (Linux, 1 core / 1.75 GB) is listed at about $13.14/mo and supports Always On and WebSockets — [Microsoft Q&A via search snippet](https://learn.microsoft.com/en-us/answers/a/12322935) (medium)
- Whether an open-but-quiet WebSocket keeps a Container Apps replica from scaling in is still not documented in what was found; with min = max = 1 the replica should not be removed, but this needs a test — [ASSUMPTION] (low)
- Fly.io shared-cpu-1x at 256 MB is about $1.94–2.02/mo (unconfirmed); 512 MB price not verified — [source: Fly pricing via search snippet](https://fly.io/docs/pricing/) (medium)

## Data & Constraints

- Runtime is .NET (`net10.0`), ASP.NET Core + SignalR; Linux hosting is viable (CI runs on ubuntu-latest) — [source: `Directory.Build.props`, `ci.yml`] (confidence: high)
- Room and connection state is held in memory (`ConcurrentDictionary` in `RoomRegistry`) → single instance only, no horizontal scaling, and a restart/scale-to-zero loses all rooms — [source: `src/Belote.Server/RoomRegistry.cs`] (confidence: high)
- No database. The only persistence is optional JSON Lines game logs under `Table:LogDirectory`; unset means none — [source: `ServerHost.cs`, `RoomRegistry.cs`] (confidence: high)
- SignalR keep-alive is 5 s with a 15 s client timeout, so a connectivity gap pauses the table within ~15 s; hosts that drop idle sockets or cold-start will disrupt play — [source: `ServerHost.cs`] (confidence: high)
- Config via environment variables works with ASP.NET Core defaults (`ASPNETCORE_URLS`, `Web__RootPath`); the default URL `http://localhost:5080` is used only when none is set — [source: `ServerHost.cs`] (confidence: high)
- A host must provide WebSocket support and HTTPS (wss) — [ASSUMPTION] (confidence: low)

## Evidence Against the Idea

- True $0 options each carry a gameplay cost: Azure F1 caps WebSockets; Render and Container Apps cold-start and, because rooms live in memory, lose in-progress games when idle/scaled to zero; Oracle can reclaim idle VMs — [sources above] (medium)
- Free-tier terms changed repeatedly in 2025–2026 (Fly dropped its old free allowance model, Hetzner repriced twice, Oracle reportedly cut limits), so "cheapest" is a moving target — [sources above] (medium)
- Paid options are only a few euros/dollars per month; the saving between "free" and ~$2–5/mo may not justify the reliability and setup trade-offs — [ASSUMPTION] (low)
- No demand evidence yet that anyone but the owner will play; hosting effort may be premature — [ASSUMPTION] (low)

## Gaps & Open Questions

- [NEEDS CLARIFICATION: Audience and expected concurrent players/rooms]
- [NEEDS CLARIFICATION: Is losing in-progress rooms on idle/restart acceptable?]
- [NEEDS CLARIFICATION: Does the server's real memory footprint fit in 256 MB (Fly) / free-tier RAM? Not measured]
- [NEEDS CLARIFICATION: Actual WebSocket-concurrency and CPU-quota limits for Azure F1 (official limits page not retrieved)]
- [NEEDS CLARIFICATION: Whether open-but-quiet SignalR connections keep a Container Apps replica "active"]
- [NEEDS CLARIFICATION: Current official Fly.io free allowance and Hetzner post-June-2026 prices]
- [NEEDS CLARIFICATION: Preferred provider/account constraints, custom domain need, CI/CD preference]

## Sources

All web sources were surfaced by web search and not fetched directly (hosts are not on the auto-fetch allowlist); treat them as snippets.

- https://learn.microsoft.com/en-us/answers/questions/1358483/cant-connect-to-free-webapp-on-azure-through-webso (host: learn.microsoft.com, policy: search snippet only)
- https://agentdeals.dev/azure-free-tier-2026 (host: agentdeals.dev, policy: search snippet only)
- https://render.com/changelog/free-web-services-now-remain-active-while-receiving-websocket-messages (host: render.com, policy: search snippet only)
- https://render.com/docs/free (host: render.com, policy: search snippet only)
- https://azure.microsoft.com/en-us/pricing/details/container-apps/ (host: azure.microsoft.com, policy: search snippet only)
- https://fly.io/docs/pricing/ (host: fly.io, policy: search snippet only)
- https://agentdeals.dev/hetzner-pricing-2026 (host: agentdeals.dev, policy: search snippet only)
- https://privatedevops.com/news/hetzner-june-2026-cloud-price-increase-what-to-do (host: privatedevops.com, policy: search snippet only)
- https://blog.51sec.org/2023/02/oracle-cloud-cleaning-up-idle-compute.html (host: blog.51sec.org, policy: search snippet only)
- https://braindetox.kr/en/posts/oracle_always_free_tier_reduced_2026.html (host: braindetox.kr, policy: search snippet only)
