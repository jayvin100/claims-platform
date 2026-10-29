using Claims.Api.Claims;

namespace Claims.Api.Notifications;

/// <summary>Which events email the Claimant, and what each email says (D20). Pure, so it is unit-tested.</summary>
public static class NotificationEmails
{
    /// <summary>Events that send no email on purpose: they aren't news to the Claimant.</summary>
    public static readonly IReadOnlySet<string> Ignored = new HashSet<string> { nameof(ReserveSet), nameof(InfoProvided) };

    /// <summary>
    /// The email for an event, or null if it is on <see cref="Ignored"/> or has no email yet. The consumer has
    /// already read the event from its JSON (ClaimEventConsumer.ReadEvent), so a malformed one never reaches here.
    /// </summary>
    public static SimulatedEmail? For(ClaimEvent claimEvent, DateTimeOffset now) => claimEvent switch
    {
        ClaimSubmitted e => Email(e, e.ClaimantId, now,
            $"We've received your Claim {e.ClaimNumber}",
            $"Thank you. Your Claim {e.ClaimNumber} has been submitted. We'll let you know when an Officer picks it up."),
        ClaimPickedUp e => Email(e, e.ClaimantId, now,
            $"Your Claim {e.ClaimNumber} is being reviewed",
            $"An Officer has picked up your Claim {e.ClaimNumber} and is now reviewing it."),
        InfoRequested e => Email(e, e.ClaimantId, now,
            $"We need more information on Claim {e.ClaimNumber}",
            $"An Officer asked: {e.Question} Please answer in the app."),
        ClaimApproved e => Email(e, e.ClaimantId, now,
            $"Your Claim {e.ClaimNumber} has been approved",
            $"Good news: your Claim {e.ClaimNumber} has been approved. We'll let you know when payment is made."),
        ClaimRejected e => Email(e, e.ClaimantId, now,
            $"Your Claim {e.ClaimNumber} has been rejected",
            $"Your Claim {e.ClaimNumber} has been rejected. Reason: {e.Reason}"),
        ClaimSettled e => Email(e, e.ClaimantId, now,
            $"Your Claim {e.ClaimNumber} has been settled",
            $"Payment for your Claim {e.ClaimNumber} has been made."),
        // Ignored, or unknown; the consumer tells the two apart.
        _ => null,
    };

    private static SimulatedEmail Email(ClaimEvent claimEvent, Guid claimantId, DateTimeOffset now, string subject, string body) =>
        new(claimEvent.EventId, claimEvent.ClaimId, claimantId, subject, body, now);
}
