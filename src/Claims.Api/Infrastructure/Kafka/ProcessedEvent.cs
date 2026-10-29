namespace Claims.Api.Infrastructure.Kafka;

/// <summary>
/// An event a consumer has already handled (D16). The key is consumer name plus event id, so Notifications
/// and Reporting each keep their own record and never suppress each other's work.
/// </summary>
public class ProcessedEvent
{
    public string Consumer { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    private ProcessedEvent() { } // for EF Core

    public ProcessedEvent(string consumer, Guid eventId, DateTimeOffset processedAt)
    {
        Consumer = consumer;
        EventId = eventId;
        ProcessedAt = processedAt;
    }
}
