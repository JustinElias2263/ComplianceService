using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.Evaluation.Events;

/// <summary>
/// Event raised when a compliance evaluation is completed
/// </summary>
public sealed class ComplianceEvaluationCompletedEvent : IDomainEvent
{
    public Guid EvaluationId { get; }
    public Guid ApplicationId { get; }
    public string Environment { get; }
    public bool Allowed { get; }
    public List<string> Violations { get; }
    public int TotalVulnerabilities { get; }
    public DateTime OccurredOn { get; }

    public ComplianceEvaluationCompletedEvent(
        Guid evaluationId,
        Guid applicationId,
        string environment,
        bool allowed,
        List<string> violations,
        int totalVulnerabilities,
        DateTime occurredOn)
    {
        EvaluationId = evaluationId;
        ApplicationId = applicationId;
        Environment = environment;
        Allowed = allowed;
        Violations = violations;
        TotalVulnerabilities = totalVulnerabilities;
        OccurredOn = occurredOn;
    }
}
