# 8.2 Glossary

[← Cheat Sheet](01-cheat-sheet.md) · [Index](../README.md)

| Term | Meaning |
|---|---|
| **ACID** | Atomicity, Consistency, Isolation, Durability — guarantees of a local database transaction |
| **Aggregate** | A cluster of domain objects changed together and treated as one consistency boundary (e.g. one order) |
| **At-least-once** | Delivery guarantee where a message may arrive more than once but is never lost |
| **At-most-once** | Delivery guarantee where a message may be lost but is never duplicated |
| **Backoff** | Increasing the wait between retries, usually exponentially |
| **Backpressure** | A slow consumer signalling upstream to slow down or stop sending |
| **BFF** | Backend for Frontend — an API layer tailored to one client type |
| **Bulkhead** | Isolating resources so that one failing part cannot exhaust what others need |
| **Cache avalanche** | Many cache entries expiring (or the cache failing) at once, overloading the database |
| **Cache penetration** | Repeated lookups for keys that don't exist, bypassing the cache every time |
| **Cache stampede** | Many requests recomputing the same expired hot key simultaneously |
| **CDC** | Change Data Capture — streaming changes by reading the database's transaction log |
| **Choreography** | Saga style where services react to each other's events with no central coordinator |
| **Circuit breaker** | A proxy that stops calls to a failing dependency and probes for recovery |
| **Claim check** | Sending a reference to data in a message instead of the data itself |
| **Compensating transaction** | A new operation that semantically reverses an earlier committed one |
| **Competing consumers** | Multiple workers reading from the same queue to process messages in parallel |
| **Consensus** | Agreement among nodes on a value despite failures (Raft, Paxos, ZAB) |
| **Consumer group** | A set of consumers that share a topic's partitions so each message is handled by one of them |
| **Coordinated omission** | Load-test error where slow responses delay subsequent requests, hiding queueing latency |
| **CQRS** | Command Query Responsibility Segregation — separate models for writes and reads |
| **Crypto-shredding** | Making data unreadable by destroying its encryption key |
| **Dead-letter queue (DLQ)** | A holding queue for messages that could not be processed after the allowed attempts |
| **Deadline propagation** | Passing the remaining time budget to downstream calls |
| **Dual write** | Writing to two systems without a shared transaction — inherently unsafe |
| **Effectively-once** | At-least-once delivery combined with idempotent processing, so the effect occurs once |
| **Empty cancel / empty rollback** | A Cancel/compensation arriving when the Try/action never executed |
| **Event** | An immutable record of something that happened, named in the past tense |
| **Event sourcing** | Persisting state as the sequence of events that produced it |
| **Eventual consistency** | Copies converge over time; reads may be temporarily stale |
| **Fail closed / fail open** | On failure of a control, deny (closed) or allow (open) the operation |
| **Fallback** | An alternative response used when the primary operation fails or is rejected |
| **Fencing token** | A monotonically increasing number used by a resource to reject requests from a stale leader |
| **Goodput** | Throughput of *useful, successful* work, as opposed to total requests handled |
| **Half-open** | Circuit-breaker state in which a limited number of probe calls test recovery |
| **Hanging reservation** | A TCC reservation for which neither Confirm nor Cancel ever arrives |
| **Hedged request** | Sending a duplicate request after a delay and using whichever answer comes first |
| **Idempotency key** | A unique client-supplied identifier that lets a server recognise retries of the same request |
| **Idempotent** | Producing the same result whether applied once or many times |
| **Inbox** | A table of processed message IDs used to detect duplicates |
| **Jitter** | Randomness added to delays or TTLs so that clients do not act in lockstep |
| **Leader election** | Choosing exactly one node to perform singleton work |
| **Lease** | A lock that expires automatically unless renewed |
| **Load levelling** | Using a queue to smooth spikes so consumers work at a steady rate |
| **Load shedding** | Deliberately rejecting some requests to protect overall service health |
| **Materialized view** | A precomputed, stored query result kept up to date as source data changes |
| **Metastable failure** | A failure state that persists after its trigger is gone, sustained by the system's own behaviour (e.g. retries) |
| **mTLS** | Mutual TLS — both client and server authenticate with certificates |
| **Natural idempotency** | An operation that is idempotent by design (setting absolute state) without extra tracking |
| **Optimistic concurrency** | Detecting conflicting updates with a version check at write time instead of locking |
| **Orchestration** | Saga style where a central coordinator tells each participant what to do |
| **Outbox** | A table in the service's own database holding events to be published |
| **Partition key** | The value that decides which partition a message goes to; preserves per-key ordering |
| **Pivot transaction** | The saga step after which the saga cannot be compensated and must complete |
| **Poison message** | A message that fails every time it is processed |
| **Projection / projector** | Code that consumes events and maintains a read model |
| **Read model** | A data representation optimised for queries (the "Q" side of CQRS) |
| **Read-your-writes** | Guarantee that a user sees their own update on their next read |
| **Rebalance** | Redistribution of partitions among consumers when group membership changes |
| **Reconciliation** | Periodically comparing two records of truth to detect and repair divergence |
| **Retry budget** | A cap on retries as a fraction of normal traffic |
| **Saga** | A sequence of local transactions with compensations in place of a distributed transaction |
| **Semantic lock** | An application-level marker (e.g. `PENDING`) indicating a record is mid-saga |
| **Single-flight** | Coalescing concurrent identical requests so only one does the work |
| **SLI / SLO** | Service Level Indicator (a measurement) / Objective (the target for it) |
| **Snapshot** | A stored copy of an aggregate's state at a version, to avoid replaying all events |
| **Split brain** | Two nodes both believing they are the leader |
| **Stale-while-revalidate** | Serving a stale cached value while refreshing it in the background |
| **Suspension (late Try)** | A TCC Try arriving after its Cancel; must be refused |
| **TCC** | Try-Confirm/Cancel — reserve resources, then finalise or release |
| **Thundering herd** | Many clients acting at the same instant and overwhelming a resource |
| **Tombstone** | A marker recording that something was cancelled/deleted, so late arrivals are rejected |
| **Two-phase commit (2PC)** | Protocol in which a coordinator asks participants to prepare, then to commit or abort |
| **Upcasting** | Transforming an old event version to the current shape at read time |
| **Visibility timeout** | Period during which a delivered message is hidden from other consumers |
| **WAL** | Write-Ahead Log — the database's durable change log, read by CDC |

---

[← Back to index](../README.md)
