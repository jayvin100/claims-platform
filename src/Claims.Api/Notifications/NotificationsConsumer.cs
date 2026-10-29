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

    protected override Task ApplyAsync(
        AppDbContext db, string eventType, string eventJson, DateTimeOffset now, CancellationToken stoppingToken)
    {
        var email = NotificationEmails.For(eventType, eventJson, now);
        if (email is not null)
        {
            db.SimulatedEmails.Add(email);
            // Logged before the save; if the save fails, the message is retried and this line appears again,
            // but the log table still gets exactly one row.
            logger.LogInformation("Simulated email to Claimant {ClaimantId}: \"{Subject}\" - {Body}",
                email.ClaimantId, email.Subject, email.Body);
        }
        return Task.CompletedTask;
    }
}
