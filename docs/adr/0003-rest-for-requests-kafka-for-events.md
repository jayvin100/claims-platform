# REST for anything a user waits on; Kafka for domain events

Every command and query is a REST call, because a user is waiting for the answer. Each change also produces a domain event, which reaches Kafka through an outbox (ADR-0004), and nobody waits on what happens next. Two consumers use those events: a notification log (simulated emails to Claimants) and a read model for Exposure and Workload. The Exposure and Workload views read that model, so they are eventually consistent: a few seconds behind the claims data.

## Considered Options

- **REST only, with the dashboard querying the claims tables directly**: rejected. It's simpler, but it leaves Kafka with nothing to do, and the brief asks us to justify a real use of both.
- **Commands over Kafka (for example, submitting a claim as a message)**: rejected. The Claimant needs an immediate answer (validation errors, a Claim Number), and turning that into request/reply over a broker adds latency and correlation logic for no gain.
- **Kafka feeding the Queue and the Claimant's timeline**: rejected. Those are acted on straight away, so they must be exact and immediate; they read live data over REST.

## Consequences

- The user flow never blocks on the broker.
- The Exposure and Workload views can briefly lag the claims data. That is acceptable for a management view, and it means Kafka does real work rather than being decorative.
