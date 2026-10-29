using System.Text;
using System.Text.Json.Nodes;
using Claims.Api.Claims;
using Claims.Api.Infrastructure.Data;
using Claims.Api.Infrastructure.Kafka;
using Claims.Api.Infrastructure.Outbox;
using Claims.Api.Notifications;
using Claims.Api.Reporting;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
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

    // Every poison message below carries this event id, so the tests can check it was never recorded anywhere.
    private static readonly Guid PoisonEventId = Guid.Parse("d0900000-0000-0000-0000-000000000001");

    // Also forgets the poison event id, so one failed run (which recorded it) can't fail every later run.
    public async Task InitializeAsync()
    {
        await ReportingTestData.ClearReadModel(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.ProcessedEvents.Where(p => p.EventId == PoisonEventId).ExecuteDeleteAsync();
        await db.SimulatedEmails.Where(e => e.EventId == PoisonEventId).ExecuteDeleteAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    // Event type header, message value, and how the dlq-reason header starts.
    public static TheoryData<string?, string?, string> PoisonMessages()
    {
        var submitted = Json(new ClaimSubmitted(PoisonEventId, Guid.NewGuid(), "CLM-SG-000001", Market.SG,
            DateTimeOffset.UtcNow, Aisha.Id, "SGD"));
        var reserveSet = Json(new ReserveSet(PoisonEventId, Guid.NewGuid(), "CLM-SG-000001", Market.SG,
            DateTimeOffset.UtcNow, 100m, "SGD"));
        return new()
        {
            { null, submitted.ToJsonString(), "no event-type header" },
            { nameof(ClaimSubmitted), null, "empty value" },
            { nameof(ClaimSubmitted), "not json", "can't read ClaimSubmitted" },
            { nameof(ClaimSubmitted), "null", "ClaimSubmitted is null" },
            { "ClaimArchived", submitted.ToJsonString(), "unknown event type ClaimArchived" },
            // A missing field would otherwise be read as null: a bogus email, and a Reporting save that fails for ever.
            { nameof(ClaimSubmitted), Without(submitted, "currency"), "can't read ClaimSubmitted" },
            { nameof(ClaimSubmitted), With(submitted, "currency", null), "can't read ClaimSubmitted" },
            { nameof(ClaimSubmitted), With(submitted, "claimId", 5), "can't read ClaimSubmitted" },
            // Notifications ignores ReserveSet, but still reads it, so a malformed one isn't recorded as processed.
            { nameof(ReserveSet), With(reserveSet, "claimId", 5), "can't read ReserveSet" },
            // Valid JSON with a value no Claim could have, which would otherwise fail on every retry.
            { nameof(ClaimSubmitted), With(submitted, "market", 99), "can't read ClaimSubmitted" },
            { nameof(ClaimSubmitted), With(submitted, "market", "99"), "can't read ClaimSubmitted" },
            // Names joined by commas read as flags: HK (2) | ID (4) = 6, which is no Market.
            { nameof(ClaimSubmitted), With(submitted, "market", "HK, ID"), "ClaimSubmitted has an unknown market" },
            { nameof(ClaimSubmitted), With(submitted, "currency", "SGDXX"), "ClaimSubmitted has a currency that isn't 3" },
            { nameof(ReserveSet), With(reserveSet, "currency", "SGDXX"), "ReserveSet has a currency that isn't 3" },
            { nameof(ClaimSubmitted), With(submitted, "claimNumber", new string('9', 300)), "ClaimSubmitted has a claim number over 20" },
        };
    }

    [Theory]
    [MemberData(nameof(PoisonMessages))]
    public async Task A_poison_message_goes_to_the_dead_letter_topic_with_its_reason_and_is_not_processed(
        string? eventType, string? value, string reason)
    {
        foreach (var consumer in Consumers())
        {
            var producer = new FakeProducer();

            await consumer.HandleAsync(Received(eventType, value), producer, CancellationToken.None);

            var deadLetter = AssertDeadLettered(producer, value, consumer);
            Assert.StartsWith(reason, Header(deadLetter, KafkaOptions.DeadLetterReasonHeader));
            if (eventType is not null) // the original headers travel with it, so it can be replayed as it was
                Assert.Equal(eventType, Header(deadLetter, KafkaOptions.EventTypeHeader));
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProcessedEvents.AnyAsync(p => p.EventId == PoisonEventId));
        Assert.False(await db.SimulatedEmails.AnyAsync(e => e.EventId == PoisonEventId));
        Assert.Equal(0, await db.ReportingClaims.CountAsync());
    }

    [Fact]
    public async Task An_event_type_header_with_a_null_value_is_poison_not_a_crash()
    {
        foreach (var consumer in Consumers())
        {
            var producer = new FakeProducer();
            var received = Received(null, "{ }");
            received.Message.Headers.Add(KafkaOptions.EventTypeHeader, null);

            await consumer.HandleAsync(received, producer, CancellationToken.None);

            var deadLetter = AssertDeadLettered(producer, "{ }", consumer);
            Assert.Equal("no event-type header", Header(deadLetter, KafkaOptions.DeadLetterReasonHeader));
        }
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

    // Only HandleAsync is tested: that the consume loop then rewinds and retries rather than commits is checked by
    // hand, as D87 says for the outbox publisher.
    [Fact]
    public async Task A_failed_dead_letter_send_throws()
    {
        var consumer = Consumers().First();

        await Assert.ThrowsAsync<ProduceException<string, string>>(() =>
            consumer.HandleAsync(Received("ClaimArchived", "{ }"), new FakeProducer(fails: true), CancellationToken.None));
    }

    // A good event when the database is down: nothing listens on port 1, so the consumer's first query fails, as it
    // would in a real outage. That is a failure that may pass, so it must throw (and be retried), not be dead-lettered.
    [Fact]
    public async Task A_database_error_is_retried_not_dead_lettered()
    {
        using var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseNpgsql("Host=localhost;Port=1;Database=none;Username=x;Password=x"))
            .BuildServiceProvider();
        var consumer = new ReportingConsumer(services.GetRequiredService<IServiceScopeFactory>(), new KafkaOptions(),
            TimeProvider.System, NullLogger<ReportingConsumer>.Instance);
        var row = OutboxMessage.From(new ClaimSubmitted(Guid.NewGuid(), Guid.NewGuid(), "CLM-SG-000001", Market.SG,
            DateTimeOffset.UtcNow, Aisha.Id, "SGD"));
        var producer = new FakeProducer();

        // EF wraps the connection error it got from Npgsql.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.HandleAsync(Received(row.Type, row.Payload), producer, CancellationToken.None));

        Assert.IsType<NpgsqlException>(error.InnerException);
        Assert.Empty(producer.Sent);
    }

    private IEnumerable<ClaimEventConsumer> Consumers() =>
    [
        ActivatorUtilities.CreateInstance<NotificationsConsumer>(factory.Services),
        ActivatorUtilities.CreateInstance<ReportingConsumer>(factory.Services),
    ];

    // An event's JSON exactly as the outbox writes it, to break one field at a time.
    private static JsonObject Json(ClaimEvent claimEvent) => JsonNode.Parse(OutboxMessage.From(claimEvent).Payload)!.AsObject();

    private static string Without(JsonObject json, string field)
    {
        var copy = json.DeepClone().AsObject();
        copy.Remove(field);
        return copy.ToJsonString();
    }

    private static string With(JsonObject json, string field, JsonNode? value)
    {
        var copy = json.DeepClone().AsObject();
        copy[field] = value;
        return copy.ToJsonString();
    }

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

    private static string Header(Message<string, string> message, string key) =>
        Encoding.UTF8.GetString(message.Headers.GetLastBytes(key));
}
