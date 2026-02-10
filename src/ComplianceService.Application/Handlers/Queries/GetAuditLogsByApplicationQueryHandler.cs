using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;
using System.Text.Json;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting audit logs by application
/// </summary>
public class GetAuditLogsByApplicationQueryHandler : IRequestHandler<GetAuditLogsByApplicationQuery, Result<IReadOnlyList<AuditLogDto>>>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IApplicationRepository _applicationRepository;

    public GetAuditLogsByApplicationQueryHandler(
        IAuditLogRepository auditLogRepository,
        IApplicationRepository applicationRepository)
    {
        _auditLogRepository = auditLogRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<IReadOnlyList<AuditLogDto>>> Handle(GetAuditLogsByApplicationQuery request, CancellationToken cancellationToken)
    {
        // Verify application exists
        var applicationResult = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (applicationResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AuditLogDto>>(applicationResult.Error);
        }

        IEnumerable<Domain.Audit.AuditLog> auditLogs;

        // Get logs based on whether environment filter is provided
        if (!string.IsNullOrWhiteSpace(request.Environment))
        {
            auditLogs = await _auditLogRepository.GetByApplicationAndEnvironmentAsync(
                request.ApplicationId,
                request.Environment,
                request.FromDate,
                request.ToDate,
                cancellationToken);
        }
        else
        {
            auditLogs = await _auditLogRepository.GetByApplicationIdAsync(
                request.ApplicationId,
                request.PageSize,
                request.PageNumber,
                cancellationToken);
        }

        // Apply date filters if provided
        if (request.FromDate.HasValue)
        {
            auditLogs = auditLogs.Where(a => a.EvaluatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            auditLogs = auditLogs.Where(a => a.EvaluatedAt <= request.ToDate.Value);
        }

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
