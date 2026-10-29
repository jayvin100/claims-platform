# Shared infrastructure

The database context, demo identity, the outbox publisher and the Kafka plumbing, used by all modules.

- `Outbox/OutboxPublisher` is the single publisher: every second it sends unsent outbox rows to `claim-events` in sequence order, one at a time, and marks each sent after Kafka acknowledges it.
- `Kafka/ClaimEventConsumer` is the base for every consumer: own group, skip already-processed event ids, save the processed id with the consumer's rows in one transaction, commit the offset after, retry a failing message.
- `Kafka/KafkaTopicCreator` creates `claim-events` in Development, retrying until Kafka is up. `Kafka:Enabled=false` switches all Kafka work off (the integration tests do).
