# ADR 0001 — Clean Architecture layering

**Status:** Accepted · 2026-06-25

## Context

The API integrates with volatile third parties (multiple FX rate sources inbound,
user webhooks outbound) and must stay testable without network access. We want the
business rules — rate aggregation and alert hysteresis — isolated from HTTP, EF Core,
and the specific upstream providers.

## Decision

Three projects, dependencies pointing inward only:

- **`FxRates.Core`** — entities, ports (interfaces), and pure algorithms
  (`RateAggregator`, `AlertEvaluator`). No external dependencies.
- **`FxRates.Infrastructure`** — adapters implementing the ports: EF Core
  `DbContext` + repositories, one `HttpClient` adapter per FX source, the webhook
  sender, the background refresh loop, the clock.
- **`FxRates.API`** — minimal-API endpoints, DI wiring, auth, OpenAPI/Scalar.

Core defines interfaces; Infrastructure implements them; the API composes everything.

## Consequences

- The pure logic is unit-tested with no I/O, and the HTTP layer is integration-tested
  through `WebApplicationFactory` with fakes wired through the ports
  (`FakeFxRateSource`, `FakeWebhookSender`, a controllable `IClock`) — no real network.
- Adding a new FX source is a new adapter; swapping the database or webhook transport
  doesn't touch Core.
- A little more ceremony (interfaces + DI) than a single project — worth it for the
  testability and clear boundaries.
