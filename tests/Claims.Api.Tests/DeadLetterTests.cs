using System.Text;
using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Kafka;
using Claims.Api.Infrastructure.Outbox;
using Claims.Api.Notifications;
using Claims.Api.Reporting;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Claims.Api.Tests.TestUsers;

namespace Claims.Api.Tests;

/// <summary>
/// D90: a message no retry could fix goes to claim-events-dlq, and the consumer moves on. Messages are handed to
/// each consumer as Kafka would deliver them, against the test database, with a fake producer in place of Kafka.
/// In the Reporting collection because a good event adds a read-model row, which the other Reporting tests total up.
/// </summary>
[Collection(ReportingCollection.Name)]
public class DeadLetterTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string ClaimKey = "claim-key";

    public Task InitializeAsync() => ReportingTestData.ClearReadModel(factory);
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(null, """{ "eventId": "8a0c2f4e-0000-0000-0000-000000000001" }""", "no event-type header")]
    [InlineData("ClaimSubmitted", null, "empty value")]
    [InlineData("ClaimSubmitted", "not json", "not JSON")]
    [InlineData("ClaimSubmitted", "{ }", "no event id")]
    public async Task An_unreadable_message_goes_to_the_dead_letter_topic_with_its_reason(
        string? eventType, string? value, string reason)
    {
        foreach (var consumer in Consumers())
        {
            var producer = new FakeProducer();

            await consumer.HandleAsync(Received(eventType, value), producer, CancellationToken.None);

            var deadLetter = AssertDeadLettered(producer, value, consumer);
            Assert.Equal(reason, Header(deadLetter, KafkaOptions.DeadLetterReasonHeader));
        }
    }

    [Fact]
    public async Task An_unknown_event_type_goes_to_the_dead_letter_topic_and_is_not_recorded_as_processed()
    {
        var eventId = Guid.NewGuid();
        var value = $$"""{ "eventId": "{{eventId}}", "claimId": "{{Guid.NewGuid()}}" }""";

        foreach (var consumer in Consumers())
        {
            var producer = new FakeProducer();

            await consumer.HandleAsync(Received("ClaimArchived", value), producer, CancellationToken.None);

            var deadLetter = AssertDeadLettered(producer, value, consumer);
            Assert.Equal("unknown event type ClaimArchived", Header(deadLetter, KafkaOptions.DeadLetterReasonHeader));
            // The original headers travel with it, so it can be replayed as it was.
            Assert.Equal("ClaimArchived", Header(deadLetter, KafkaOptions.EventTypeHeader));
        }
        await AssertNotProcessed(eventId);
    }

    [Fact]
    public async Task An_event_whose_JSON_does_not_fit_its_type_goes_to_the_dead_letter_topic()
    {
        var eventId = Guid.NewGuid();
        // A number where the Claim id should be: valid JSON with an event id, but no retry could ever read it.
        var value = $$"""{ "eventId": "{{eventId}}", "claimId": 5 }""";

        foreach (var consumer in Consumers())
        {
            var producer = new FakeProducer();

            await consumer.HandleAsync(Received(nameof(ClaimSubmitted), value), producer, CancellationToken.None);

            var deadLetter = AssertDeadLettered(producer, value, consumer);
            Assert.Equal("JSON doesn't fit ClaimSubmitted", Header(deadLetter, KafkaOptions.DeadLetterReasonHeader));
        }
        await AssertNotProcessed(eventId);
    }

    [Fact]
    public async Task A_good_event_is_applied_and_nothing_is_dead_lettered()
    {
        var claimId = Guid.NewGuid();
        var submitted = new ClaimSubmitted(Guid.NewGuid(), claimId, "CLM-SG-000001", Market.SG,
            DateTimeOffset.UtcNow, Aisha.Id, "SGD");
        var row = OutboxMessage.From(submitted);

        foreach (var consumer in Consumers())
        {
            var producer = new FakeProducer();
            await consumer.HandleAsync(Received(row.Type, row.Payload), producer, CancellationToken.None);
            Assert.Empty(producer.Sent);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.ReportingClaims.AnyAsync(r => r.ClaimId == claimId));
        Assert.True(await db.SimulatedEmails.AnyAsync(e => e.EventId == submitted.EventId));
        Assert.Equal(2, await db.ProcessedEvents.CountAsync(p => p.EventId == submitted.EventId));
    }

    [Fact]
    public async Task A_failed_dead_letter_send_throws_so_the_message_is_retried_not_committed()
    {
        var consumer = Consumers().First();

        // The consume loop catches this, rewinds and retries, and never reaches its Commit.
        await Assert.ThrowsAsync<ProduceException<string, string>>(() =>
            consumer.HandleAsync(Received("ClaimArchived", "{ }"), new FakeProducer(fails: true), CancellationToken.None));
    }

    private IEnumerable<ClaimEventConsumer> Consumers() =>
    [
        ActivatorUtilities.CreateInstance<NotificationsConsumer>(factory.Services),
        ActivatorUtilities.CreateInstance<ReportingConsumer>(factory.Services),
    ];

    // A message as the consumer would receive it from claim-events, partition 2, offset 42. Kafka hands over an
    // empty value as null, hence the "!".
    private static ConsumeResult<string, string> Received(string? eventType, string? value)
    {
        var headers = new Headers { { "trace-id", Encoding.UTF8.GetBytes("abc") } };
        if (eventType is not null)
            headers.Add(KafkaOptions.EventTypeHeader, Encoding.UTF8.GetBytes(eventType));
        return new ConsumeResult<string, string>
        {
            TopicPartitionOffset = new TopicPartitionOffset(KafkaOptions.Topic, new Partition(2), new Offset(42)),
            Message = new Message<string, string> { Key = ClaimKey, Value = value!, Headers = headers },
        };
    }

    // Exactly one copy sent to the dead-letter topic, with the original key, value and headers, plus the dlq headers.
    private static Message<string, string> AssertDeadLettered(FakeProducer producer, string? value, ClaimEventConsumer consumer)
    {
        var (topic, message) = Assert.Single(producer.Sent);
        Assert.Equal(KafkaOptions.DeadLetterTopic, topic);
        Assert.Equal((ClaimKey, value), (message.Key, message.Value));
        Assert.Equal("abc", Header(message, "trace-id"));
        var expectedConsumer = consumer is ReportingConsumer ? ReportingConsumer.Name : NotificationsConsumer.Name;
        Assert.Equal(expectedConsumer, Header(message, KafkaOptions.DeadLetterConsumerHeader));
        Assert.Equal("claim-events/2/42", Header(message, KafkaOptions.DeadLetterSourceHeader));
        return message;
    }

    private async Task AssertNotProcessed(Guid eventId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProcessedEvents.AnyAsync(p => p.EventId == eventId));
        Assert.Equal(0, await db.ReportingClaims.CountAsync());
    }

    private static string Header(Message<string, string> message, string key) =>
        Encoding.UTF8.GetString(message.Headers.GetLastBytes(key));
}
