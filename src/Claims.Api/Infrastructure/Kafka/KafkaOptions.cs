using Confluent.Kafka;

namespace Claims.Api.Infrastructure.Kafka;

/// <summary>The "Kafka" settings section, plus the names every producer and consumer agrees on (D14).</summary>
public class KafkaOptions
{
    public const string Section = "Kafka";

    /// <summary>The one topic every domain event goes to, keyed by Claim id (ADR-0005).</summary>
    public const string Topic = "claim-events";

    /// <summary>
    /// Where a consumer puts a message it can't handle, so the main topic never stalls (D90). Kept for a person to
    /// inspect and replay; nothing reads it automatically.
    /// </summary>
    public const string DeadLetterTopic = "claim-events-dlq";

    /// <summary>The message header that carries the event type, so a consumer knows how to read the JSON.</summary>
    public const string EventTypeHeader = "event-type";

    /// <summary>Headers a dead letter carries on top of the original ones: who sent it there, why, and where it came from.</summary>
    public const string DeadLetterConsumerHeader = "dlq-consumer";
    public const string DeadLetterReasonHeader = "dlq-reason";
    public const string DeadLetterSourceHeader = "dlq-source";

    /// <summary>
    /// Off switches off all Kafka work: topic creation, the outbox publisher and the consumers.
    /// Integration tests turn it off, so they never need a broker; events still reach the outbox table.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How long topic creation and the consumers wait before trying again after a failure.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>The connection settings every producer, consumer and admin client starts from.</summary>
    // IPv4 only: "localhost" resolves to IPv6 first on macOS, where Docker's Kafka refuses the connection,
    // so every connect would log an error before falling back to IPv4.
    // Reconnect at most about once a second (default 100 ms): while Kafka is down each failed connect is
    // logged, and the default rate buried the API's own lines (about 1,200 lines in a 30 s outage).
    public ClientConfig ClientConfig() => new()
    {
        BootstrapServers = BootstrapServers,
        BrokerAddressFamily = BrokerAddressFamily.V4,
        ReconnectBackoffMs = 1000,
    };

    /// <summary>The settings for every producer: the outbox publisher and the consumers' dead-letter sends.</summary>
    public ProducerConfig ProducerConfig() => new(ClientConfig())
    {
        // Acknowledged only once the broker has stored the message, so "sent" really means stored.
        Acks = Acks.All,
        // Give up on a send after 10 s (the default is 5 minutes), so while Kafka is down the caller
        // logs a warning and retries instead of waiting silently.
        MessageTimeoutMs = 10_000,
    };
}
