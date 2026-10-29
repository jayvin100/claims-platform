using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Api.Claims;

namespace Claims.Api.Infrastructure.Outbox;

/// <summary>
/// A domain event waiting to be published to Kafka (ADR-0004). It is saved in the same transaction as the
/// Claim change, so a crash can delay an event but never lose it.
/// </summary>
public class OutboxMessage
{
    /// <summary>Auto-incrementing, so the publisher sends events in the order they were saved (timestamps can tie).</summary>
    public long Sequence { get; private set; }
    public Guid EventId { get; private set; }
    public string Type { get; private set; } = null!;
    public Guid ClaimId { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; } // empty until Kafka acknowledges it

    private OutboxMessage() { } // for EF Core

    /// <summary>Called by the publisher once Kafka has acknowledged the event.</summary>
    public void MarkSent(DateTimeOffset now) => SentAt = now;

    /// <summary>How events are written to JSON, and so how consumers read them back.</summary>
    // camelCase names and enum names, the same JSON the API returns.
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static OutboxMessage From(ClaimEvent claimEvent) => new()
    {
        EventId = claimEvent.EventId,
        Type = claimEvent.GetType().Name,
        ClaimId = claimEvent.ClaimId,
        // The runtime type, so the subtype's own fields (e.g. Currency) are included.
        Payload = JsonSerializer.Serialize(claimEvent, claimEvent.GetType(), JsonOptions),
        OccurredAt = claimEvent.OccurredAt,
    };
}
