using Confluent.Kafka;

namespace Claims.Api.Infrastructure.Kafka;

/// <summary>The "Kafka" settings section, plus the names every producer and consumer agrees on (D14).</summary>
public class KafkaOptions
{
    public const string Section = "Kafka";

    /// <summary>The one topic every domain event goes to, keyed by Claim id (ADR-0005).</summary>
    public const string Topic = "claim-events";

    /// <summary>The message header that carries the event type, so a consumer knows how to read the JSON.</summary>
    public const string EventTypeHeader = "event-type";

    /// <summary>
    /// Off switches off all Kafka work: topic creation, the outbox publisher and the consumers.
    /// Integration tests turn it off, so they never need a broker; events still reach the outbox table.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>The connection settings every producer, consumer and admin client starts from.</summary>
    // IPv4 only: "localhost" resolves to IPv6 first on macOS, where Docker's Kafka refuses the connection,
    // so every connect would log an error before falling back to IPv4.
    public ClientConfig ClientConfig() => new()
    {
        BootstrapServers = BootstrapServers,
        BrokerAddressFamily = BrokerAddressFamily.V4,
    };
}
