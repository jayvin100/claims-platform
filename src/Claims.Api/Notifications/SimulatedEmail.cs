namespace Claims.Api.Notifications;

/// <summary>
/// A row in the notification log: an email we would have sent to the Claimant (D20). Nothing is really sent;
/// the row and a console line stand in for it.
/// </summary>
public class SimulatedEmail
{
    public long Id { get; private set; }
    public Guid EventId { get; private set; } // the event that caused it
    public Guid ClaimId { get; private set; }
    public Guid ClaimantId { get; private set; } // the recipient
    public string Subject { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public DateTimeOffset LoggedAt { get; private set; }

    private SimulatedEmail() { } // for EF Core

    public SimulatedEmail(Guid eventId, Guid claimId, Guid claimantId, string subject, string body, DateTimeOffset loggedAt)
    {
        EventId = eventId;
        ClaimId = claimId;
        ClaimantId = claimantId;
        Subject = subject;
        Body = body;
        LoggedAt = loggedAt;
    }
}
