# FX Rate Aggregator API — Design Spec

**Date:** 2026-06-25
**Project:** Portfolio Project #2 (`fx-rates-api`)
**Status:** Approved design — ready for implementation plan

## Purpose

A USD↔PHP FX rate aggregator REST API. Pulls rates from multiple third-party
sources, aggregates them, serves fast cached reads, stores history, and fires
user webhooks on threshold crossings. The portfolio goal: demonstrate the skills
Project #1 (receipts-api) did not — **third-party integration + failure handling**
(inbound source pulls with fallback, outbound webhook delivery with retry).

Stack/conventions match #1 exactly (see `../../HANDOFF.md` and the receipts-api
README): .NET 10 + ASP.NET Core minimal APIs, Clean Architecture, EF Core, xUnit
integration tests via `WebApplicationFactory`, Docker, GitHub Actions CI, deploy =
Render (web) + Neon (Postgres), Scalar docs, `X-Forwarded-Proto` fix behind proxy.

## Scope (v1)

In: aggregated current rate, convert, history, alerts-via-webhook, JWT for alerts.
Pair scoped to **USD↔PHP only** (YAGNI; provider abstraction allows more later).
Out: email/SMS alerts, multi-pair matrix, API-key model (reserved for Project #3).

## Architecture — Clean Architecture (mirrors #1)

**Solution `FxRatesAPI.slnx`** (.NET 10), three projects:

- **`FxRates.Core`** — no external deps.
  - Entities: `RateSnapshot`, `SourceRate`, `Alert`, `AlertDelivery`, `User`.
  - Ports: `IFxRateSource`, `IRateSnapshotRepository`, `IAlertRepository`,
    `IUserRepository`, `IWebhookSender`, `IClock`, `IPasswordHasher`, `ITokenIssuer`.
  - Pure algorithms (the #2 equivalents of #1's `SettlementCalculator`):
    - `RateAggregator` — list of source rates → `{ median, mean, min, max }`.
    - `AlertEvaluator` — `(alert, newRate, priorState) → shouldFire?` with hysteresis.
- **`FxRates.Infrastructure`** — EF Core `DbContext` + repositories; one `HttpClient`
  adapter per FX source implementing `IFxRateSource`; `WebhookSender`; the hosted
  `RateRefreshService`; BCrypt + JWT adapters.
- **`FxRates.API`** — minimal-API endpoints, DI wiring, JWT auth, Scalar/OpenAPI,
  `X-Forwarded-Proto` forwarded-headers fix copied from #1, startup auto-migrate
  (non-Testing env only), Render `postgres://` → Npgsql conversion + `PORT` honoring.

## FX sources (v1)

All free and **keyless** (no secrets to manage on Render free tier):

- `open.er-api.com` (`/v6/latest/USD`)
- `floatrates.com` (`/daily/usd.json`)
- fawazahmed0 `currency-api` (jsDelivr-hosted)

Each is a distinct `IFxRateSource` adapter. Adding key-based sources later is just a
new adapter + a secret. (Aggregation here is about resilience/agreement across
sources, not arbitrage — the rates will be close; the point is the mechanics.)

## Endpoints

**Public:**
- `GET /health` — liveness.
- `GET /rates` — latest snapshot:
  `{ asOf, base, quote, aggregate:{median,mean,min,max}, sources:[{name,rate,fetchedAt,status}] }`.
- `GET /rates/history?from=&to=&limit=` — aggregate rate time series.
- `GET /convert?amount=&direction=USD_TO_PHP|PHP_TO_USD` —
  `{ amount, rate, result, asOf }`, using the median aggregate of the latest snapshot.

**Auth (JWT, register/login pattern reused from #1):**
- `POST /auth/register` (201; 409 on duplicate), `POST /auth/login` (JWT).
- `POST /alerts` — body `{ comparator: ">="|"<=", threshold, callbackUrl }`. The monitored
  quantity is always the latest **USD→PHP median rate** (PHP per 1 USD); the alert fires
  when that rate satisfies `comparator threshold`.
- `GET /alerts` — caller's alerts.
- `DELETE /alerts/{id}`.

Rates + convert are public (easy live demo, no token). Alert endpoints
`RequireAuthorization` (401 without token).

## Data flow

**Refresh loop — `RateRefreshService` (hosted `BackgroundService`, interval configurable, default 10 min):**
1. Fetch all sources concurrently. Each fetch wrapped with timeout + retry/backoff
   (Polly via `Microsoft.Extensions.Http.Resilience`).
2. **Fallback:** drop sources that fail; aggregate from survivors. If **all** fail,
   keep serving the last good cached snapshot marked stale (`asOf` unchanged) — reads
   never 500 on an upstream outage. Per-source `status` (`ok`/`failed`) surfaced in `/rates`.
3. `RateAggregator` (pure) computes median/mean/min/max over survivor rates.
4. Persist one `RateSnapshot` (+ child `SourceRate`s) → this **is** the history.
   Update the in-memory "latest" cache.
5. Evaluate active alerts against the new aggregate → fire webhooks for those that cross.

**Reads** (`/rates`, `/convert`) serve from the in-memory cache — fast, never hit
upstream inline. `/rates/history` queries Postgres. Median is the headline rate.

## Alerts & webhook delivery

- `AlertEvaluator` (pure): `(alert, newRate, priorState)`. **Hysteresis** — fires once
  when the condition becomes true; re-arms only when the condition goes false again
  (no re-firing every refresh while still crossed).
- `WebhookSender` (`IWebhookSender`): outbound POST of the triggered-alert payload with
  timeout + exponential-backoff retry (Polly). Each attempt recorded as an
  `AlertDelivery` row (`status`, `attempts`, `lastError`). Demonstrates outbound
  third-party integration + resilience, complementing the inbound source pulls.

## Error handling summary

- Single source down → excluded, aggregate continues.
- All sources down → last good snapshot served stale; never a 5xx from upstream.
- Bad request params (unknown direction, negative amount) → 400 with problem details.
- Auth missing/invalid → 401.
- Webhook endpoint down → retries with backoff; failure recorded, refresh loop unaffected.

## Testing (mirrors #1: xUnit + `WebApplicationFactory`, "Testing" env skips Npgsql, SQLite in-memory)

- **Unit:** `RateAggregator` (median/mean/min/max; single survivor; empty input) and
  `AlertEvaluator` (fire on cross; hysteresis re-arm; no-refire while crossed) — pure, no I/O.
- **Integration** (fakes wired through the ports; *no real network in tests*):
  - `FakeFxRateSource` — scripted rates + forced failures.
  - `FakeWebhookSender` — captures deliveries.
  - Controllable `IClock`.
  - Cases: `/rates` aggregation + per-source status; all-sources-down serves stale;
    `/convert` math both directions; `/rates/history`; alert endpoints 401 without token;
    alert CRUD; end-to-end "refresh crosses threshold → webhook fired once (hysteresis)".

The fakes are the payoff of the port design — same lesson #1 demonstrated.

## Deploy (identical playbook to #1)

- `Dockerfile` (multi-stage); `.github/workflows/ci.yml` (`dotnet test`); keepalive
  Action pinging `/health` every 12 min — **also keeps the background refresh alive**
  on Render's free tier (instance sleeps after ~15 min idle otherwise).
- Render Docker web service + **Neon** Postgres. `render.yaml` blueprint. Auto-migrate
  on startup (non-Testing). `X-Forwarded-Proto` fix so Scalar works behind the proxy.
- Secrets: only `Jwt__Key` + `DATABASE_URL` (FX sources are keyless). Refresh interval
  via config/env (`Fx__RefreshIntervalMinutes`).
- `README.md` + `docs/adr/0001-layering.md` + MIT `LICENSE`.

## Repo isolation (IMPORTANT)

`projects/fx-rates-api/` has its own `.git`, nested inside the private finance dir but
isolated. Only ever `git push` from inside `fx-rates-api`, never the parent. Commit
identity: `suzerai1302` + noreply email (work email must not enter history).
