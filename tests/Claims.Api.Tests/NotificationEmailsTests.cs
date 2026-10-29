using Claims.Api.Claims;
using Claims.Api.Infrastructure.Outbox;
using Claims.Api.Notifications;

namespace Claims.Api.Tests;

/// <summary>
/// Which events email the Claimant, read from the same JSON the outbox writes. The Kafka consumer around it
/// is checked by hand (spec: Testing Decisions).
/// </summary>
public class NotificationEmailsTests
{
    private static readonly Guid ClaimantId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid OfficerId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid ClaimId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 2, 0, 0, TimeSpan.Zero);
    private const string ClaimNumber = "CLM-SG-000123";

    public static TheoryData<ClaimEvent> EventsTheClaimantHearsAbout() =>
    [
        new ClaimSubmitted(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId, "SGD"),
        new ClaimPickedUp(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, OfficerId, ClaimantId),
        new ClaimApproved(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId),
        new ClaimRejected(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId, "Not covered"),
        new ClaimSettled(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId),
    ];

    [Theory]
    [MemberData(nameof(EventsTheClaimantHearsAbout))]
    public void Claimant_is_emailed_about_the_event_with_its_Claim_Number(ClaimEvent claimEvent)
    {
        var email = EmailFor(claimEvent);

        Assert.NotNull(email);
        Assert.Equal(ClaimantId, email.ClaimantId);
        Assert.Equal(claimEvent.EventId, email.EventId);
        Assert.Equal(ClaimId, email.ClaimId);
        Assert.Contains(ClaimNumber, email.Subject);
    }

    [Fact]
    public void Rejection_email_gives_the_Officers_reason()
    {
        var email = EmailFor(new ClaimRejected(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId, "Not covered"));

        Assert.Contains("Reason: Not covered", email!.Body);
    }

    [Fact]
    public void ReserveSet_sends_no_email()
    {
        Assert.Null(EmailFor(new ReserveSet(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, 1000m, "SGD")));
    }

    // Through the outbox row, so the test reads exactly the type and JSON a consumer receives from Kafka.
    private static SimulatedEmail? EmailFor(ClaimEvent claimEvent)
    {
        var row = OutboxMessage.From(claimEvent);
        return NotificationEmails.For(row.Type, row.Payload, Now);
    }
}
