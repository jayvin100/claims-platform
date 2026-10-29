using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Claims.Api.Infrastructure.Kafka;

/// <summary>
/// Creates the claim-events topic and its dead-letter topic in Development (D25, D90). It runs in the background
/// and retries until Kafka is reachable, so the API starts and serves REST even while Kafka is down.
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
        TopicSpecification[] topics =
        [
            new() { Name = KafkaOptions.Topic, NumPartitions = Partitions, ReplicationFactor = Replicas },
            // Dead letters are rare and only read by a person, so one partition is enough.
            new() { Name = KafkaOptions.DeadLetterTopic, NumPartitions = 1, ReplicationFactor = Replicas },
        ];
        var names = string.Join(", ", topics.Select(t => t.Name));

        while (true)
        {
            try
            {
                // Without a timeout the request waits up to a minute for a broker that isn't there.
                await admin.CreateTopicsAsync(topics, new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
                logger.LogInformation("Created Kafka topics {Topics}", names);
                return;
            }
            catch (CreateTopicsException ex)
                when (ex.Results.All(r => r.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
            {
                // Every start after the first lands here, and that's success. So does a start where only one
                // topic was missing (such as the dead-letter topic on a stack created before it existed).
                logger.LogInformation("Kafka topics {Topics} are ready", names);
                return;
            }
            catch (KafkaException ex)
            {
                logger.LogWarning("Couldn't create Kafka topics {Topics} ({Reason}); retrying in {Delay}s",
                    names, ex.Message, KafkaOptions.RetryDelay.TotalSeconds);
                await Task.Delay(KafkaOptions.RetryDelay, stoppingToken);
            }
        }
    }
}
