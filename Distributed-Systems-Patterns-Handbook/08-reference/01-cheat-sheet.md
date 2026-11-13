# 8.1 Cheat Sheet

[← Production Readiness Checklist](../07-cross-cutting/04-production-readiness-checklist.md) · [Index](../README.md) · Next: [Glossary →](02-glossary.md)

All the comparison tables in one place.

---

## 1. The 12 Patterns (plus 5)

| # | Pattern | Category | Core mechanism | Use when | Chapter |
|---|---|---|---|---|---|
| 1 | Event Sourcing | Data | Immutable sequence of events instead of overwriting rows | Finance, audit, full history, time-travel debugging | [2.5](../02-data-consistency/05-event-sourcing-cqrs-materialized-views.md) |
| 2 | Circuit Breaker | Resilience | Opens after repeated failures; fails fast; probes for recovery | Prevent cascading timeouts between services | [4.2](../04-resilience/02-circuit-breaker.md) |
| 3 | Retry with Backoff | Resilience | Exponentially growing delays + random jitter | Transient network errors, without a thundering herd | [4.1](../04-resilience/01-retry-backoff-jitter.md) |
| 4 | Bulkhead | Resilience | Isolated thread/connection/concurrency pool per dependency | A slow integration must not starve critical paths | [4.3](../04-resilience/03-bulkhead.md) |
| 5 | Rate Limiting | Traffic | Cap requests per client per window (token bucket, sliding window) | Abuse, spikes, noisy neighbours | [4.4](../04-resilience/04-rate-limiting.md) |
| 6 | Cache-Aside | Performance | Check cache → load DB on miss → invalidate on write | Read-heavy, infrequently updated data | [5.1](../05-performance-api-coordination/01-cache-aside.md) |
| 7 | Idempotent Consumer | Messaging | Track processed message IDs; ignore duplicates | Any at-least-once broker; prevent double charges | [3.1](../03-messaging/01-idempotent-consumer.md) |
| 8 | Backend for Frontend | API | Separate tailored API layer per client type | Shape payloads, aggregate calls, reduce mobile round-trips | [5.2](../05-performance-api-coordination/02-backend-for-frontend.md) |
| 9 | Database per Service | Data | Private schema/store owned by one service | Loose coupling; independent deploy and scale | [2.4](../02-data-consistency/04-database-per-service.md) |
| 10 | Leader Election | Coordination | Lease via consensus store picks one active node | Singleton jobs: cron, batch coordinators | [5.3](../05-performance-api-coordination/03-leader-election.md) |
| 11 | Competing Consumers | Messaging | Many workers on one queue | Scale throughput horizontally | [3.2](../03-messaging/02-competing-consumers.md) |
| 12 | Materialized View | Performance | Precomputed denormalized results updated by events | Replace cross-service joins with single-table reads | [2.5](../02-data-consistency/05-event-sourcing-cqrs-materialized-views.md) |
| + | Transactional Outbox | Data | Event row in the same transaction; async relay | Any service that must publish reliably | [2.1](../02-data-consistency/01-transactional-outbox.md) |
| + | Saga | Data | Local transactions + compensations | Multi-service business transaction | [2.2](../02-data-consistency/02-saga-pattern.md) |
| + | TCC | Data | Reserve → confirm / cancel | Money, stock, seats — resources that can be held | [2.3](../02-data-consistency/03-try-confirm-cancel.md) |
| + | Dead-Letter Queue | Messaging | Park messages that exceed retry limit | Isolate poison messages | [3.3](../03-messaging/03-dead-letter-queues.md) |
| + | CQRS | Data | Separate write model and read models | Read and write needs diverge | [2.5](../02-data-consistency/05-event-sourcing-cqrs-materialized-views.md) |

---

## 2. Distributed Transaction Options

| | 2PC / XA | Saga | TCC | Outbox |
|---|---|---|---|---|
| Solves | Atomic commit across resources | Multi-step business transaction | Multi-step with reservation | Atomic "write + publish" |
| Consistency | Strong | Eventual | Eventual, reserved state | Eventual |
| Isolation | Yes | No | Partial | n/a |
| Failure handling | Abort | Compensate | Cancel | Retry publish |
| Participant API | XA | action + compensate | try + confirm + cancel | outbox table |
| Blocking | Yes | No | Business hold with TTL | No |

## 3. Saga: Orchestration vs Choreography

| | Orchestration | Choreography |
|---|---|---|
| Control | Central state machine | Distributed event reactions |
| Visibility | High | Low |
| Coupling | Orchestrator → all | Services ↔ events |
| Best for | 4+ steps, branching, timers | 2–3 linear steps |

## 4. Idempotency Strategies

| Strategy | Best used when… | Pros | Cons |
|---|---|---|---|
| Inbox table (event ID) | Non-repeatable math or any DB effect | Works for any DB operation; effect applied once | Extra table; cleanup |
| Natural idempotency | Status changes, overwrites | Zero storage; fastest | Not for relative increments |
| API idempotency key | External HTTP gateways | Provider replays first response | Must persist and reuse the same key |

## 5. Saga / TCC Edge Cases

| Case | Cause | Guard |
|---|---|---|
| Empty cancel / compensation | Cancel before Try | Record `CANCELLED` tombstone |
| Late Try (suspension) | Try after Cancel | Refuse on tombstone |
| Hanging reservation | No Confirm/Cancel arrives | TTL + reaper |
| Duplicate phase call | Retry | Guarded status transition |

## 6. Outbox Relay

| | Polling | CDC (Debezium) |
|---|---|---|
| Latency | Poll interval | Near real time |
| DB load | Queries per batch | Log read only |
| Ordering | Approximate (use partition key) | Commit order |
| Ops | Small worker | Connect cluster + slot monitoring |

## 7. Resilience Patterns Compared

| Pattern | Question it answers | Trigger | Response |
|---|---|---|---|
| Timeout | How long do I wait? | Elapsed time | Abort the call |
| Retry | Was it a blip? | Transient error | Try again, later |
| Circuit breaker | Is it down? | Failure/slow rate over a window | Stop calling; fallback |
| Bulkhead | How much can it consume? | Concurrency limit reached | Reject/queue extra calls |
| Rate limiter | Is this client over quota? | Requests per window | 429 |
| Load shedder | Am I overloaded? | Server saturation | 503 by priority |
| Fallback | What do I return instead? | Any of the above | Degraded answer |

## 8. Thundering Herd Roles

| Pattern | Role |
|---|---|
| Backoff + jitter | **Stops the burst** — spreads retries |
| Rate limiting / load shedding | **Controls the flow** — drops excess at the edge |
| Bulkhead | **Contains damage** — caps concurrency |
| Circuit breaker (half-open) | **Tests recovery** — a few probes first |
| Single-flight / TTL jitter | **Removes duplicate work** at the cache |

## 9. Transactional Outbox vs Bulkhead

| | Outbox | Bulkhead |
|---|---|---|
| Layer | Data | Compute |
| Problem | Lost event after DB commit | One slow dependency exhausts shared resources |
| Solution | Event in same transaction + relay | Isolated pools per dependency |
| Protects | Consistency | Availability |

## 10. Circuit Breaker Fallbacks

| Strategy | Example | Caution |
|---|---|---|
| Degrade | Hide recommendations | UI must handle absence |
| Stale cache | Last known rate | Bound staleness; key by user/tenant |
| Default | Standard tax rate | Reconcile later |
| Alternate provider | Secondary gateway | Needs own breaker and capacity |
| Queue for later | Charge when gateway returns | Idempotent; tell the user |
| Fail fast | 503 + Retry-After | Right when wrong answer > no answer |
| **Security checks** | **Deny** | **Never fail open** |

## 11. Rate-Limiting Algorithms

| Algorithm | Bursts | Memory | Note |
|---|---|---|---|
| Fixed window | 2× at boundary | Tiny | Simplest |
| Sliding window counter | Smooth | Tiny | Good default |
| Sliding window log | Exact | High | Costly |
| Token bucket | Up to bucket size | Tiny | Most common |
| Leaky bucket | Smoothed out | Queue | Traffic shaping |

## 12. Broker DLQ Support

| Broker | DLQ |
|---|---|
| SQS | Native (`maxReceiveCount`, redrive) |
| RabbitMQ | Native (dead-letter exchange; delivery limit on quorum queues) |
| Azure Service Bus | Native (`MaxDeliveryCount`, sub-queue) |
| Kafka | Convention in client/framework (dead-letter topic) |

## 13. Leader Election Mechanisms

| Mechanism | Safety |
|---|---|
| etcd / ZooKeeper / Consul / Kubernetes Lease | Consensus-backed |
| Database lease / advisory lock | As strong as the DB |
| Redis `SET NX PX` | Best-effort — add fencing or idempotency |

## 14. Cache Problems

| Problem | Cause | Fix |
|---|---|---|
| Stampede | Hot key expires | Single-flight, jitter, stale-while-revalidate |
| Penetration | Lookups of non-existent keys | Negative cache, Bloom filter |
| Avalanche | Mass expiry / cache restart | TTL jitter, warm-up, DB protection |
| Stale set | Read/write race | TTL, versioned set, CDC invalidation |

## 15. Delivery Guarantees

| Guarantee | Achieved by | Risk |
|---|---|---|
| At-most-once | Ack before processing | Loss |
| At-least-once | Ack after processing | Duplicates |
| Effectively-once | At-least-once + idempotent handler | Needs discipline |

## 16. Formulas

| Purpose | Formula |
|---|---|
| In-flight requests | `rate × latency` |
| Workers needed | `rate × processing time ÷ target utilisation` |
| Chain availability | `A₁ × A₂ × … × Aₙ` |
| P(any of *n* calls slow) | `1 − (1 − p)ⁿ` |
| Full-jitter delay | `random(0, min(cap, base × 2^attempt))` |
| Cache effective latency | `cache + (1 − hitRatio) × db` |
| Retry amplification | `attempts ^ layers` |
| Threads held during outage | `rate × timeout` |

## 17. Tooling

| Need | Tools |
|---|---|
| Durable sagas/workflows | Temporal, Camunda, Step Functions |
| Outbox via CDC | Debezium |
| Outbox + inbox + sagas (framework) | MassTransit, NServiceBus, Axon, Eventuate |
| Resilience library | Resilience4j, Polly, Opossum |
| Coordination | etcd, ZooKeeper, Consul, Kubernetes Lease |

---

Next: [Glossary →](02-glossary.md)
