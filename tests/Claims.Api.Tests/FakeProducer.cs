using Confluent.Kafka;

namespace Claims.Api.Tests;

/// <summary>
/// Stands in for Kafka when a consumer sends a dead letter: it keeps what was sent, or fails like a broker that's
/// down. Confluent's IProducer is already an interface, so no wrapper is needed (D87). Only ProduceAsync is used.
/// </summary>
public class FakeProducer(bool fails = false) : IProducer<string, string>
{
    public List<(string Topic, Message<string, string> Message)> Sent { get; } = [];

    public Task<DeliveryResult<string, string>> ProduceAsync(
        string topic, Message<string, string> message, CancellationToken cancellationToken = default)
    {
        if (fails)
            throw new ProduceException<string, string>(new Error(ErrorCode.Local_MsgTimedOut), new DeliveryResult<string, string>());
        Sent.Add((topic, message));
        return Task.FromResult(new DeliveryResult<string, string> { Topic = topic, Message = message });
    }

    // Nothing below is used by the consumers.
    public Task<DeliveryResult<string, string>> ProduceAsync(
        TopicPartition topicPartition, Message<string, string> message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public void Produce(string topic, Message<string, string> message, Action<DeliveryReport<string, string>>? deliveryHandler = null) =>
        throw new NotSupportedException();
    public void Produce(TopicPartition topicPartition, Message<string, string> message, Action<DeliveryReport<string, string>>? deliveryHandler = null) =>
        throw new NotSupportedException();
    public int Poll(TimeSpan timeout) => throw new NotSupportedException();
    public int Flush(TimeSpan timeout) => throw new NotSupportedException();
    public void Flush(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void InitTransactions(TimeSpan timeout) => throw new NotSupportedException();
    public void BeginTransaction() => throw new NotSupportedException();
    public void CommitTransaction(TimeSpan timeout) => throw new NotSupportedException();
    public void CommitTransaction() => throw new NotSupportedException();
    public void AbortTransaction(TimeSpan timeout) => throw new NotSupportedException();
    public void AbortTransaction() => throw new NotSupportedException();
    public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout) =>
        throw new NotSupportedException();
    public Handle Handle => throw new NotSupportedException();
    public string Name => "fake";
    public int AddBrokers(string brokers) => throw new NotSupportedException();
    public void SetSaslCredentials(string username, string password) => throw new NotSupportedException();
    public void Dispose() { }
}
