namespace Claims.Api.Claims;

/// <summary>One line of a Claim's timeline: the status after an action, and what happened (D20).</summary>
public class ClaimHistoryEntry
{
    public long Id { get; private set; } // database-generated, so entries sort in the order they were saved
    public ClaimStatus Status { get; private set; }
    public string Description { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }

    private ClaimHistoryEntry() { } // for EF Core

    internal ClaimHistoryEntry(ClaimStatus status, string description, DateTimeOffset occurredAt)
    {
        Status = status;
        Description = description;
        OccurredAt = occurredAt;
    }
}
