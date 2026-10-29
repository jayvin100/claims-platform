# Transactional outbox between the database and Kafka

Domain events are written to an outbox table in the same database transaction as the Claim change, and a background worker publishes pending rows to Kafka and marks them sent. With the outbox, a REST request never touches the broker, and a crash between saving and publishing can delay an event but not lose it. It closes that one gap; it still relies on Postgres being durable, the worker retrying, Kafka acknowledging and retaining messages, and the consumers running.

## Considered Options

- **Publish straight after `SaveChanges`**: rejected. If the process dies between the save and the publish, the event is lost and the Exposure and Workload views drift silently. It also makes the REST request wait on (and fail with) the broker.
- **Change data capture (for example, Debezium reading the Postgres log)**: rejected. It solves the same problem with another piece of infrastructure to run and explain.

## Consequences

- Delivery is at-least-once: the worker can publish a row and die before marking it sent. Every event carries a unique id and each consumer skips ids it has already processed.
- Outbox rows carry an auto-incrementing sequence number, and the single worker publishes in that order (see ADR-0005).
- Events reach Kafka a moment after the change is saved, which adds to the lag in the Exposure and Workload views.
