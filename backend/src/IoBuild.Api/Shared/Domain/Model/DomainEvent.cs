namespace IoBuild.Api.Shared.Domain.Model;

/// <summary>
/// Shared kernel: base domain event for future outbox / dispatch.
/// Current IntegrationDispatchQueue already models durable integration events;
/// this type makes the intent explicit for upcoming courses (IoT, DevSecOps)
/// without changing wire behavior.
/// </summary>
public abstract record DomainEvent(DateTimeOffset OccurredAt)
{
    public Guid EventId { get; } = Guid.NewGuid();
}
