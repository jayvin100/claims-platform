namespace Claims.Api.Infrastructure.Kafka;

/// <summary>
/// A message a consumer can never handle, however often it retries: it can't be read, or its event type is one
/// the consumer doesn't know. <see cref="ClaimEventConsumer"/> sends it to the dead-letter topic instead of retrying
/// (D90). The message is the short reason that goes in the dlq-reason header.
/// </summary>
public class PoisonMessageException(string reason) : Exception(reason);
