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

    private static readonly ClaimEvent[] Emailed =
    [
        new ClaimSubmitted(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId, "SGD"),
        new ClaimPickedUp(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, OfficerId, ClaimantId),
        new InfoRequested(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId, "Please send the repair quote."),
        new ClaimApproved(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId),
        new ClaimRejected(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId, "Not covered"),
        new ClaimSettled(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId),
    ];

    private static readonly ClaimEvent[] NotEmailed =
    [
        new ReserveSet(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, 1000m, "SGD"),
        new InfoProvided(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now),
    ];

    public static TheoryData<ClaimEvent> EventsTheClaimantHearsAbout() => new(Emailed);
    public static TheoryData<ClaimEvent> EventsTheClaimantIsNotToldAbout() => new(NotEmailed);

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
    public void Info_request_email_gives_the_Officers_question()
    {
        var email = EmailFor(new InfoRequested(Guid.NewGuid(), ClaimId, ClaimNumber, Market.SG, Now, ClaimantId, "Please send the repair quote."));

        Assert.Contains("Please send the repair quote.", email!.Body);
    }

    [Theory]
    [MemberData(nameof(EventsTheClaimantIsNotToldAbout))]
    public void ReserveSet_and_InfoProvided_send_no_email_and_are_ignored_on_purpose(ClaimEvent claimEvent)
    {
        Assert.Null(EmailFor(claimEvent));
        Assert.Contains(claimEvent.GetType().Name, NotificationEmails.Ignored);
    }

    // The consumer skips an unknown event type rather than retrying it, so this test is what catches a new
    // event that nobody mapped: every ClaimEvent type must either send an email or be on the ignored list.
    [Fact]
    public void Every_event_type_is_either_emailed_or_ignored()
    {
        ClaimEvent[] oneOfEach = [.. Emailed, .. NotEmailed];
        var everyEventType = typeof(ClaimEvent).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ClaimEvent)))
            .Select(t => t.Name);

        // A new event type fails here until it is added to one of the two lists above.
        Assert.Equal(everyEventType.Order(), oneOfEach.Select(e => e.GetType().Name).Order());
        foreach (var claimEvent in oneOfEach)
        {
            var name = claimEvent.GetType().Name;
            bool emailed = EmailFor(claimEvent) is not null, ignored = NotificationEmails.Ignored.Contains(name);
            Assert.True(emailed != ignored, $"{name} must be emailed or ignored, not {(emailed ? "both" : "neither")}");
        }
    }

    // Through the outbox row, so the test reads exactly the type and JSON a consumer receives from Kafka.
    private static SimulatedEmail? EmailFor(ClaimEvent claimEvent)
    {
        var row = OutboxMessage.From(claimEvent);
        return NotificationEmails.For(row.Type, row.Payload, Now);
    }
}
