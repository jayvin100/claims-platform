using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Claims.Api.Infrastructure.Kafka;

/// <summary>
/// Creates the claim-events topic in Development (D25). It runs in the background and retries until Kafka
/// is reachable, so the API starts and serves REST even while Kafka is down.
/// </summary>
public class KafkaTopicCreator(KafkaOptions kafka, ILogger<KafkaTopicCreator> logger) : BackgroundService
{
    // Three partitions, so keying by Claim id really matters: a Claim's events always land on the same one.
    // One replica, because the Compose Kafka is a single broker.
    private const int Partitions = 3;
    private const short Replicas = 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig(kafka.ClientConfig())).Build();
        var topic = new TopicSpecification { Name = KafkaOptions.Topic, NumPartitions = Partitions, ReplicationFactor = Replicas };

        while (true)
        {
            try
            {
                // Without a timeout the request waits up to a minute for a broker that isn't there.
                await admin.CreateTopicsAsync([topic], new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
                logger.LogInformation("Created Kafka topic {Topic}", KafkaOptions.Topic);
                return;
            }
            catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                // Every start after the first lands here, and that's success.
                logger.LogInformation("Kafka topic {Topic} already exists", KafkaOptions.Topic);
                return;
            }
            catch (KafkaException ex)
            {
                logger.LogWarning("Couldn't create Kafka topic {Topic} ({Reason}); retrying in {Delay}s",
                    KafkaOptions.Topic, ex.Message, KafkaOptions.RetryDelay.TotalSeconds);
                await Task.Delay(KafkaOptions.RetryDelay, stoppingToken);
            }
        }
    }
}
