using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Kafka;

namespace Claims.Api.Notifications;

/// <summary>
/// Turns claim events into simulated emails to the Claimant: a notification log row plus a console line (D20).
/// It only knows what the events tell it, never the Claims tables (ADR-0001). The delivery rules
/// (own group, skip duplicates, commit after saving, retry on failure) are in <see cref="ClaimEventConsumer"/>.
/// </summary>
public class NotificationsConsumer(
    IServiceScopeFactory scopes, KafkaOptions kafka, TimeProvider time, ILogger<NotificationsConsumer> logger)
    : ClaimEventConsumer(scopes, kafka, time, logger)
{
    protected override string ConsumerName => "notifications";

    protected override Task<Action?> ApplyAsync(
        AppDbContext db, Guid eventId, string eventType, string eventJson, DateTimeOffset now, CancellationToken stoppingToken)
    {
        var email = NotificationEmails.For(eventType, eventJson, now);
        if (email is null)
        {
            // An unknown type is skipped (recorded as processed), not retried: a retry would block every Claim on
            // its partition. NotificationEmailsTests fails for any event type that is neither mapped nor ignored.
            if (!NotificationEmails.Ignored.Contains(eventType))
                logger.LogWarning("{Consumer} has no email for unknown event type {EventType} {EventId}; skipped",
                    ConsumerName, eventType, eventId);
            return Task.FromResult<Action?>(null);
        }

        db.SimulatedEmails.Add(email);
        // The console line is printed only after the row is saved, so a retried save never prints it twice.
        return Task.FromResult<Action?>(() => logger.LogInformation(
            "Simulated email to Claimant {ClaimantId}: \"{Subject}\" - {Body}", email.ClaimantId, email.Subject, email.Body));
    }
}
