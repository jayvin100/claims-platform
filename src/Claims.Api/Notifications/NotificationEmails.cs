using System.Text.Json;
using Claims.Api.Claims;
using Claims.Api.Infrastructure.Outbox;

namespace Claims.Api.Notifications;

/// <summary>Which events email the Claimant, and what each email says (D20). Pure, so it is unit-tested.</summary>
public static class NotificationEmails
{
    /// <summary>Events that send no email on purpose: they aren't news to the Claimant.</summary>
    public static readonly IReadOnlySet<string> Ignored = new HashSet<string> { nameof(ReserveSet), nameof(InfoProvided) };

    /// <summary>The email for an event, or null if it is on <see cref="Ignored"/> or its type is unknown.</summary>
    public static SimulatedEmail? For(string eventType, string eventJson, DateTimeOffset now) => eventType switch
    {
        nameof(ClaimSubmitted) => Email<ClaimSubmitted>(eventJson, now, e => (e.ClaimantId,
            $"We've received your Claim {e.ClaimNumber}",
            $"Thank you. Your Claim {e.ClaimNumber} has been submitted. We'll let you know when an Officer picks it up.")),
        nameof(ClaimPickedUp) => Email<ClaimPickedUp>(eventJson, now, e => (e.ClaimantId,
            $"Your Claim {e.ClaimNumber} is being reviewed",
            $"An Officer has picked up your Claim {e.ClaimNumber} and is now reviewing it.")),
        nameof(InfoRequested) => Email<InfoRequested>(eventJson, now, e => (e.ClaimantId,
            $"We need more information on Claim {e.ClaimNumber}",
            $"An Officer asked: {e.Question} Please answer in the app.")),
        nameof(ClaimApproved) => Email<ClaimApproved>(eventJson, now, e => (e.ClaimantId,
            $"Your Claim {e.ClaimNumber} has been approved",
            $"Good news: your Claim {e.ClaimNumber} has been approved. We'll let you know when payment is made.")),
        nameof(ClaimRejected) => Email<ClaimRejected>(eventJson, now, e => (e.ClaimantId,
            $"Your Claim {e.ClaimNumber} has been rejected",
            $"Your Claim {e.ClaimNumber} has been rejected. Reason: {e.Reason}")),
        nameof(ClaimSettled) => Email<ClaimSettled>(eventJson, now, e => (e.ClaimantId,
            $"Your Claim {e.ClaimNumber} has been settled",
            $"Payment for your Claim {e.ClaimNumber} has been made.")),
        // Ignored, or unknown; the consumer tells the two apart.
        _ => null,
    };

    // Reads the event as its own type (the JSON the outbox wrote), then builds the email from it.
    private static SimulatedEmail Email<TEvent>(
        string eventJson, DateTimeOffset now, Func<TEvent, (Guid ClaimantId, string Subject, string Body)> write)
        where TEvent : ClaimEvent
    {
        var claimEvent = JsonSerializer.Deserialize<TEvent>(eventJson, OutboxMessage.JsonOptions)!;
        var (claimantId, subject, body) = write(claimEvent);
        return new SimulatedEmail(claimEvent.EventId, claimEvent.ClaimId, claimantId, subject, body, now);
    }
}
