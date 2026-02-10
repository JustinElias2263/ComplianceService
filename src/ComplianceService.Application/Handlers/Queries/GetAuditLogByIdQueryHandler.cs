using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;
using System.Text.Json;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting an audit log by ID
/// </summary>
public class GetAuditLogByIdQueryHandler : IRequestHandler<GetAuditLogByIdQuery, Result<AuditLogDto>>
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditLogByIdQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<AuditLogDto>> Handle(GetAuditLogByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await _auditLogRepository.GetByIdAsync(request.AuditLogId, cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<AuditLogDto>(result.Error);
        }

        var log = result.Value;

        // Parse EvaluationId from string to Guid
        Guid.TryParse(log.EvaluationId, out var evaluationGuid);

        // Combine evidence into a single JSON string
        var completeEvidence = JsonSerializer.Serialize(new
        {
            ScanResults = log.Evidence.ScanResultsJson,
            PolicyInput = log.Evidence.PolicyInputJson,
            PolicyOutput = log.Evidence.PolicyOutputJson,
            CapturedAt = log.Evidence.CapturedAt
        });

        var dto = new AuditLogDto
        {
            Id = log.Id,
            EvaluationId = evaluationGuid,
            ApplicationName = log.ApplicationName,
            Environment = log.Environment,
            Timestamp = log.EvaluatedAt,
            DecisionAllow = log.Allowed,
            Violations = log.Violations.ToList(),
            PolicyPackage = "compliance.default",
            TotalVulnerabilities = log.TotalVulnerabilityCount,
            CriticalCount = log.CriticalCount,
            HighCount = log.HighCount,
            InitiatedBy = "system",
            CompleteEvidence = completeEvidence
        };

        return Result.Success(dto);
    }
}
