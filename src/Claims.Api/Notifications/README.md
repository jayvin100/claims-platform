# Notifications module

Consumes claim events from Kafka and writes simulated emails to the Claimant. Never reads Claims tables (ADR-0001).

`NotificationsConsumer` (group `notifications`) writes each email to the `notification_log` table and the console. `NotificationEmails` decides which events send one and what it says. ReserveSet and InfoProvided are on its ignored list; an unknown event type is logged as a warning and skipped, and a unit test fails if any event type is neither emailed nor ignored.
