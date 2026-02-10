using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;
using System.Text.Json;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting audit logs with critical vulnerabilities
/// </summary>
public class GetCriticalVulnerabilitiesQueryHandler : IRequestHandler<GetCriticalVulnerabilitiesQuery, Result<IReadOnlyList<AuditLogDto>>>
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetCriticalVulnerabilitiesQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<IReadOnlyList<AuditLogDto>>> Handle(GetCriticalVulnerabilitiesQuery request, CancellationToken cancellationToken)
    {
        var auditLogs = await _auditLogRepository.GetWithCriticalVulnerabilitiesAsync(
            request.Since,
            cancellationToken);

        var dtos = auditLogs
            .OrderByDescending(a => a.EvaluatedAt)
            .Select(log =>
            {
                Guid.TryParse(log.EvaluationId, out var evaluationGuid);

                var completeEvidence = JsonSerializer.Serialize(new
                {
                    ScanResults = log.Evidence.ScanResultsJson,
                    PolicyInput = log.Evidence.PolicyInputJson,
                    PolicyOutput = log.Evidence.PolicyOutputJson,
                    CapturedAt = log.Evidence.CapturedAt
                });

                return new AuditLogDto
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
            }).ToList();

        return Result.Success<IReadOnlyList<AuditLogDto>>(dtos);
    }
}
