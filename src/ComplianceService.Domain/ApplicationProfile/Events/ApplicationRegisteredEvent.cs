using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.ApplicationProfile.Events;

/// <summary>
/// Event raised when a new application is registered
/// </summary>
public sealed class ApplicationRegisteredEvent : IDomainEvent
{
    public Guid ApplicationId { get; }
    public string ApplicationName { get; }
    public string Owner { get; }
    public DateTime OccurredOn { get; }

    public ApplicationRegisteredEvent(
        Guid applicationId,
        string applicationName,
        string owner,
        DateTime occurredOn)
    {
        ApplicationId = applicationId;
        ApplicationName = applicationName;
        Owner = owner;
        OccurredOn = occurredOn;
    }
}
