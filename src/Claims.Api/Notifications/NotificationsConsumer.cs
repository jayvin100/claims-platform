using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Kafka;

namespace Claims.Api.Notifications;

/// <summary>
/// Turns claim events into simulated emails to the Claimant: a notification log row plus a console line (D20).
/// It only knows what the events tell it, never the Claims tables (ADR-0001). The delivery rules (own group,
/// skip duplicates, commit after saving, retry on failure, dead-letter poison) are in <see cref="ClaimEventConsumer"/>.
/// </summary>
public class NotificationsConsumer(
    IServiceScopeFactory scopes, KafkaOptions kafka, TimeProvider time, ILogger<NotificationsConsumer> logger)
    : ClaimEventConsumer(scopes, kafka, time, logger)
{
    /// <summary>The consumer group, and the name its processed event ids are recorded under.</summary>
    public const string Name = "notifications";

    protected override string ConsumerName => Name;

    protected override Task<Action?> ApplyAsync(
        AppDbContext db, Guid eventId, string eventType, string eventJson, DateTimeOffset now, CancellationToken stoppingToken)
    {
        var email = NotificationEmails.For(eventType, eventJson, now);
        if (email is null)
        {
            // An unknown type goes to the dead-letter topic, not recorded as processed (D90), so the partition keeps
            // moving. NotificationEmailsTests fails for any event type that is neither mapped nor ignored.
            if (!NotificationEmails.Ignored.Contains(eventType))
                throw new PoisonMessageException($"unknown event type {eventType}");
            return Task.FromResult<Action?>(null);
        }

        db.SimulatedEmails.Add(email);
        // The console line is printed only after the row is saved, so a retried save never prints it twice.
        return Task.FromResult<Action?>(() => logger.LogInformation(
            "Simulated email to Claimant {ClaimantId}: \"{Subject}\" - {Body}", email.ClaimantId, email.Subject, email.Body));
    }
}
