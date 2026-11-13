# 6.1 E-commerce Checkout — The Patterns in One Request

[← Leader Election](../05-performance-api-coordination/03-leader-election.md) · [Index](../README.md) · Next: [Frameworks and Tooling →](02-frameworks-and-tooling.md)

The patterns do not live in isolation. Edge patterns protect incoming traffic; data and
messaging patterns coordinate the backend. This chapter follows one "Buy Now" click end to end.

---

## 1. Architecture

```mermaid
flowchart TD
    CL["Mobile app / Web browser"]
    CL -->|"(1) Rate limiting"| GW["API Gateway"]
    GW --> BFF["(2) Backend for Frontend"]
    BFF <-->|"(3) Cache-aside"| REDIS[("Redis<br/>product + cart")]
    BFF -->|"(4) Bulkhead + circuit breaker"| ORD

    subgraph ORDSVC["Order Service - (5) database per service"]
        ORD["Order API"] -->|"(6) one local ACID transaction"| ODB[("Orders DB<br/>orders + outbox")]
    end

    ODB -->|"(7) outbox relay - Debezium CDC"| K[["Message broker"]]

    K --> PW["(8) Competing consumers<br/>3 x Payment workers"]
    PW -->|"(9) idempotent consumer - inbox"| PDB[("Payments DB")]
    PW -->|"idempotency key"| STRIPE["Payment gateway"]
    PW -->|"(10) retry with backoff, then"| DLQ[["Dead-letter queue"]]

    K --> MV["(11) Materialized view updater"]
    MV --> RDB[("Order history read DB")]
    BFF -->|"My Orders query"| RDB

    K --> SAGA["Order saga orchestrator"]
    SAGA --> INV["Inventory Service"]
    SAGA --> SHIP["Shipping Service"]

    LE["(12) Leader-elected jobs:<br/>reservation reaper, saga recovery"] -.-> SAGA
```

## 2. Happy Path

```mermaid
sequenceDiagram
    autonumber
    actor U as Shopper
    participant G as Gateway
    participant B as BFF
    participant R as Redis
    participant O as Order Service
    participant D as Orders DB
    participant K as Broker
    participant P as Payment Worker
    participant S as Payment Gateway
    participant V as Order History View

    U->>G: POST /checkout (Idempotency-Key: K1)
    G->>G: rate limit check - allowed
    G->>B: forward
    B->>R: GET cart (cache-aside)
    R-->>B: cart
    B->>O: create order (inside bulkhead + breaker, with deadline)
    O->>D: BEGIN, INSERT order PENDING, INSERT outbox OrderCreated, COMMIT
    O-->>B: 202 Accepted, orderId 42
    B-->>U: order received - status PENDING

    D-->>K: CDC publishes OrderCreated (key = 42)
    par payment
        K->>P: OrderCreated
        P->>P: inbox insert msg id (first time)
        P->>S: charge with idempotency key order-42
        S-->>P: succeeded
        P->>K: PaymentSucceeded (via its own outbox)
    and read model
        K->>V: OrderCreated - upsert row
    end
    K->>V: PaymentSucceeded - status PAID
    U->>B: GET /my-orders
    B->>V: single-table read
    V-->>B: order 42 PAID
    B-->>U: order list in milliseconds
```

## 3. Step-by-Step

| # | Stage | Patterns | What they guarantee |
|---|---|---|---|
| 1 | **Edge** | [Rate limiting](../04-resilience/04-rate-limiting.md) | Bots and runaway clients are dropped before they cost anything |
| 2 | **API shaping** | [BFF](../05-performance-api-coordination/02-backend-for-frontend.md) + [Cache-aside](../05-performance-api-coordination/01-cache-aside.md) | One compact call from the device; cart/product reads don't hit the DB |
| 3 | **Synchronous protection** | [Bulkhead](../04-resilience/03-bulkhead.md) + [Circuit breaker](../04-resilience/02-circuit-breaker.md) | Slow recommendations can't block checkout; a down tax API returns a fallback rate at once |
| 4 | **Atomic persistence** | [Database per service](../02-data-consistency/04-database-per-service.md) + [Transactional outbox](../02-data-consistency/01-transactional-outbox.md) | The order and its `OrderCreated` event are saved together — no lost orders |
| 5 | **Parallel, safe processing** | [Competing consumers](../03-messaging/02-competing-consumers.md) + [Idempotent consumer](../03-messaging/01-idempotent-consumer.md) | Payment scales horizontally; redeliveries never double-charge |
| 6 | **Failure handling** | [Retry with backoff](../04-resilience/01-retry-backoff-jitter.md) + [DLQ](../03-messaging/03-dead-letter-queues.md) | Gateway blips are retried; poison messages are isolated |
| 7 | **Fast reads** | [Materialized view](../02-data-consistency/05-event-sourcing-cqrs-materialized-views.md) | "My Orders" is a single-table read, no cross-service joins |
| 8 | **Business transaction** | [Saga](../02-data-consistency/02-saga-pattern.md) (+ [TCC](../02-data-consistency/03-try-confirm-cancel.md) for stock holds) | Stock, payment and shipping either all complete or are compensated |
| 9 | **Housekeeping** | [Leader election](../05-performance-api-coordination/03-leader-election.md) | Reaper and recovery jobs run on exactly one node |

---

## 4. Failure Walkthroughs

### 4a. Payment gateway times out

```mermaid
sequenceDiagram
    autonumber
    participant K as Broker
    participant P as Payment Worker
    participant S as Payment Gateway
    participant Q as DLQ
    K->>P: OrderCreated (order 42)
    P->>S: charge, key order-42
    S--xP: timeout (outcome unknown)
    P->>P: backoff 0.4 s (jittered)
    P->>S: charge, SAME key order-42
    S-->>P: succeeded (gateway replays the first result)
    Note over P: No double charge - the idempotency key made the retry safe
    Note over P,Q: Had all attempts failed with a transient error,<br/>the message would be redelivered later and finally dead-lettered
```

### 4b. What each failure looks like to the shopper

| Failure | Patterns that respond | Shopper sees |
|---|---|---|
| Bot flood on `/checkout` | Rate limit, load shedding | Normal experience; bots get 429 |
| Redis down | Cache-aside falls through to DB behind a breaker | Slightly slower pages |
| Recommendation service slow | Bulkhead full → empty fallback | Page without recommendations |
| Tax API down | Breaker open → cached/default rate, order flagged | Checkout works; tax reconciled later |
| Order Service crashes after commit | Outbox row is durable; relay publishes on recovery | Order appears; payment slightly delayed |
| Broker down | Outbox accumulates; relay resumes | Order stays PENDING longer |
| Payment worker crashes mid-message | Redelivery + inbox dedup + gateway key | Charged exactly once |
| Card declined | Business rejection → `PaymentDeclined` event → saga compensates stock | "Payment failed, please try another card" |
| Malformed event after a bad deploy | Permanent error → DLQ → alert → fix → replay | Delay for affected orders only |
| Shipping can't schedule | Saga compensation: refund, release stock | Order cancelled with refund notice |
| Read model lagging | Read-your-writes token / command response used for the confirmation page | Confirmation page is correct immediately |
| Service recovers after outage | Jitter + half-open probes + bulkhead | Gradual recovery, no second outage |

---

## 5. Security View of the Flow

```mermaid
flowchart LR
    U["User"] -->|"TLS, session cookie<br/>HttpOnly Secure SameSite"| GW["Gateway<br/>WAF, rate limit, authn"]
    GW -->|"validated identity"| BFF["BFF<br/>CSRF check, input validation,<br/>field allow-list"]
    BFF -->|"mTLS + user token"| OS["Order Service<br/>authorizes user for this cart"]
    OS -->|"least-privilege DB role"| DB[("Orders DB<br/>encrypted at rest")]
    DB -->|"CDC role: outbox table only"| K[["Broker<br/>TLS, per-topic ACLs"]]
    K -->|"consume-only credential"| PW["Payment worker<br/>validates schema + tenant"]
    PW -->|"tokenised card reference only"| PG["Payment gateway"]
```

| Trust boundary | Control |
|---|---|
| Internet → gateway | TLS, WAF, bot detection, per-identity rate limits |
| Browser → BFF | Tokens kept server-side; cookie flags; CSRF defence |
| BFF → services | Service identity (mTLS) **and** end-user identity; each service re-authorizes |
| Service → DB | Per-service credentials; no cross-service table access |
| DB → broker | Minimal event payloads (IDs, amounts — never card numbers); CDC limited to the outbox table |
| Broker → consumers | Per-topic ACLs; schema validation; tenant check |
| Worker → payment gateway | Tokenised payment method; idempotency key; secrets from a vault |
| Operators → DLQ / replay / breaker overrides | Authorization + audit trail |

Client-side idempotency also matters: the `Idempotency-Key` header on `POST /checkout`
prevents a double order when the shopper double-taps or the app retries.

Full treatment: [Security](../07-cross-cutting/01-security.md).

## 6. Performance View of the Flow

| Path | Budget (illustrative) | What keeps it there |
|---|---|---|
| `POST /checkout` (synchronous part) | ~150–300 ms | Only one local DB transaction on the critical path; payment is asynchronous |
| Cart/product read | ~5 ms | Cache-aside at high hit ratio |
| Order → event on broker | < 1 s | CDC relay |
| Payment completion | 1–3 s | Competing consumers scaled on lag |
| "My Orders" | ~10 ms | Materialized view, single indexed read |

Design choices that buy this:

- **Return `202 Accepted` early.** The shopper is not kept waiting on the payment gateway.
- **Asynchronous by default** after the first commit — the broker absorbs spikes (load levelling).
- **Scale each stage independently:** BFF on request rate, payment workers on queue age,
  read DB on query load.
- **Every synchronous hop has a timeout** derived from an end-to-end deadline.

The numbers are illustrative targets, not measurements. Derive real budgets from your own SLOs
— see [Performance](../07-cross-cutting/02-performance.md).

## 7. What to Watch

One dashboard row per stage:

| Stage | Golden signal |
|---|---|
| Edge | 429 ratio, request rate by client type |
| BFF | p99 latency, degraded-response ratio |
| Sync calls | Breaker state, bulkhead saturation |
| Order DB | Commit latency, **outbox oldest-unpublished age** |
| Broker | Consumer lag / age of oldest message |
| Payment | Success ratio, retry ratio, duplicate-skip count |
| DLQ | Depth (alert at > 0) |
| Read model | Projection lag |
| Saga | In-flight age, compensation rate, `NEEDS_ATTENTION` count |
| Business | Orders created vs orders paid — the end-to-end truth |

The last row is the most valuable alert in the system: if *orders created* and *orders paid*
diverge, something in the pipeline is broken, whichever component it is.

---

Next: [Frameworks and Tooling →](02-frameworks-and-tooling.md)
