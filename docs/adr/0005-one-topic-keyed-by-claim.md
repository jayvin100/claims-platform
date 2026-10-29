# One Kafka topic, keyed by Claim id

All domain events go to a single `claim-events` topic, keyed by Claim id, with the event type in a message header. Kafka only orders messages within a partition, and a key always lands on the same partition, so every event for one Claim arrives in the order it happened. The Exposure and Workload model depends on that order (a Claim must be picked up before it is settled).

The key only controls which partition a message lands on. End-to-end order also depends on the publisher and the consumers, so the guarantee rests on three things together: a single outbox publisher that sends rows in sequence order and stops and retries on failure; the Claim id key; and consumers that handle each partition's messages one at a time and commit offsets only after processing. Running several publishers would break this and is out of scope.

## Considered Options

- **A topic per event type**: rejected. Events for the same Claim would sit on different topics with no ordering between them, so a consumer could see "settled" before "picked up".
