using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.ApplicationProfile.Events;

/// <summary>
/// Event raised when an environment configuration is updated
/// </summary>
public sealed class EnvironmentConfigUpdatedEvent : IDomainEvent
{
    public Guid ApplicationId { get; }
    public string EnvironmentName { get; }
    public DateTime OccurredOn { get; }

    public EnvironmentConfigUpdatedEvent(
        Guid applicationId,
        string environmentName,
        DateTime occurredOn)
    {
        ApplicationId = applicationId;
        EnvironmentName = environmentName;
        OccurredOn = occurredOn;
    }
}
