# 06: Events reach Kafka; Claimants get simulated emails

**What to build:** Every event saved to the outbox reaches Kafka within a couple of seconds, and the Claimant gets a simulated email for each step they care about. A developer can submit a Claim in Swagger and see the email line appear in the API console. Duplicate deliveries never produce a second email.

**Blocked by:** 02: Claimant submits and tracks a Claim

**Status:** ready-for-agent

- [ ] On startup in Development, the API creates the `claim-events` topic if it doesn't exist
- [ ] A background worker polls the outbox every second, publishes unsent rows in the order they happened (key = Claim id, value = event JSON, event type in a header), and marks each sent only after Kafka acknowledges it
- [ ] If Kafka is down, REST requests still succeed and the worker catches up when it returns
- [ ] The Notifications consumer uses its own consumer group and commits offsets only after handling a message
- [ ] For ClaimSubmitted, ClaimPickedUp, InfoRequested, ClaimApproved, ClaimRejected and ClaimSettled, it writes a simulated email to the Claimant into a notification log table and the console; it ignores ReserveSet and InfoProvided
- [ ] It records processed event ids in the same transaction as the log row and skips ids it has already seen
- [ ] The outbox worker can be switched off for integration tests
- [ ] Every NuGet package (Confluent.Kafka) is confirmed with Jay before it's added
- [ ] Manual check: submit a Claim and see the simulated email in the console
