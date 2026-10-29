namespace Claims.Api.Claims;

/// <summary>
/// A question the Assigned Officer asks the Claimant, and the Claimant's answer. It is open until answered.
/// Answered ones stay on the Claim, so the whole conversation can be read back.
/// </summary>
public class InfoRequest
{
    public long Id { get; private set; } // database-generated, so Info Requests sort in the order they were asked
    public string Question { get; private set; } = null!;
    public DateTimeOffset RequestedAt { get; private set; }
    public string? Answer { get; private set; } // empty while the Info Request is open
    public DateTimeOffset? AnsweredAt { get; private set; }

    public bool IsOpen => Answer is null; // no setter, so EF Core doesn't map it

    private InfoRequest() { } // for EF Core

    internal InfoRequest(string question, DateTimeOffset requestedAt)
    {
        Question = question;
        RequestedAt = requestedAt;
    }

    internal void AnswerWith(string answer, DateTimeOffset answeredAt)
    {
        Answer = answer;
        AnsweredAt = answeredAt;
    }
}
