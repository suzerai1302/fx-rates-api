# FX Rate Aggregator API

A resilient REST API that aggregates **USD↔PHP** exchange rates from multiple third-party
sources, serves fast cached reads with history, and fires user webhooks when a rate
crosses a threshold. Built with ASP.NET Core and Entity Framework Core.

> **Live demo:** _set your Render URL here after deploying_ · **Interactive API docs:** `/scalar`
>
> _Hosted on Render's free tier — the first request after ~15 min idle takes ~50s to wake, then it's fast._

![CI](https://github.com/suzerai1302/fx-rates-api/actions/workflows/ci.yml/badge.svg)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Why this project

Exchange-rate data lives behind third-party APIs that go down, rate-limit, or disagree.
This service demonstrates **third-party integration with real failure handling**: it pulls
from several sources, aggregates what's healthy, keeps serving through outages, and
delivers outbound webhooks with retries — the parts of backend work that are easy to
demo but hard to get right.

## Features

- **Multi-source aggregation** — pulls 3 keyless FX sources concurrently each refresh and reports median / mean / min / max
- **Resilient by design** — per-source timeout + retry + circuit breaker; a down source is dropped, and if *all* fail the last good snapshot is served stale (reads never 5xx)
- **Rate history** — every refresh is persisted; query the time series
- **Currency conversion** — USD↔PHP off the latest median
- **Threshold alerts + webhooks** — register an alert; when the rate crosses, your callback URL is POSTed once (hysteresis), with retry/backoff and per-delivery records
- **JWT auth** — register / login, BCrypt-hashed passwords; alert endpoints are protected
- **Interactive docs** — OpenAPI + Scalar UI
- **Fully tested** — pure-logic unit tests + integration tests over real HTTP, no network

## Tech stack

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core (.NET 10), Minimal APIs |
| Persistence | Entity Framework Core + PostgreSQL |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly) |
| Auth | JWT bearer, BCrypt password hashing |
| Docs | OpenAPI + Scalar |
| Tests | xUnit + `WebApplicationFactory`, SQLite in-memory |
| CI / Deploy | GitHub Actions · Render (Docker) + Neon (Postgres) |

## Architecture

Clean Architecture — dependencies point inward only ([ADR](docs/adr/0001-layering.md)):

- **`FxRates.Core`** — entities, ports, and the pure algorithms `RateAggregator` and `AlertEvaluator`
- **`FxRates.Infrastructure`** — EF Core, the FX source adapters, the webhook sender, the background refresh loop
- **`FxRates.API`** — endpoints, DI, auth, OpenAPI

A hosted `RateRefreshService` fetches all sources on an interval, aggregates the
survivors, persists a snapshot, updates an in-memory cache, then evaluates alerts.
Reads serve from the cache and never call upstream inline.

## Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/health` | — | Liveness |
| GET | `/rates` | — | Latest snapshot: aggregate + per-source status |
| GET | `/rates/history?from=&to=&limit=` | — | Aggregate time series |
| GET | `/convert?amount=&direction=USD_TO_PHP\|PHP_TO_USD` | — | Convert off the latest median |
| POST | `/auth/register` | — | Create account (201; 409 if exists) |
| POST | `/auth/login` | — | Get a JWT |
| POST | `/alerts` | JWT | Create a threshold alert `{ comparator, threshold, callbackUrl }` |
| GET | `/alerts` | JWT | List your alerts |
| DELETE | `/alerts/{id}` | JWT | Delete your alert |

The monitored quantity for alerts is the latest **USD→PHP median** (PHP per 1 USD); an
alert fires when that rate satisfies `comparator threshold`.

## Run locally

```bash
# needs the .NET 10 SDK
dotnet test                                   # run the test suite
dotnet run --project src/FxRates.API          # needs a Postgres connection (see below)
```

The API uses Postgres outside the test environment. Provide a connection string via
`ConnectionStrings__Postgres` or a `DATABASE_URL` (a `postgres://…` URL is parsed
automatically), and a signing key via `Jwt__Key`. Tests need neither — they run on
SQLite in-memory with fakes.

## Deploy (Render + Neon)

1. Create a free **Neon** Postgres project; copy its connection string.
2. In **Render**, create a **Blueprint** from this repo (`render.yaml`) — a free Docker
   web service. Paste the Neon string as `DATABASE_URL`; `Jwt__Key` is generated.
3. After it's live, set the repo Actions **variable** `HEALTHCHECK_URL` to the Render
   base URL so the keepalive workflow pings `/health` every 12 minutes (also keeps the
   refresh loop warm). Then drop the URL into the badge line at the top.

Migrations apply automatically on startup; the `X-Forwarded-Proto` fix ensures the
OpenAPI/Scalar docs work behind Render's TLS proxy.

## License

MIT — see [LICENSE](LICENSE).
