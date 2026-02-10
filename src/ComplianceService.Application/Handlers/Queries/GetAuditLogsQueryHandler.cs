using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;
using System.Text.Json;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting audit logs with filtering and pagination
/// </summary>
public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<IReadOnlyList<AuditLogDto>>>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IApplicationRepository _applicationRepository;

    public GetAuditLogsQueryHandler(
        IAuditLogRepository auditLogRepository,
        IApplicationRepository applicationRepository)
    {
        _auditLogRepository = auditLogRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<IReadOnlyList<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        // Build search query
        IEnumerable<Domain.Audit.AuditLog> auditLogs;

        if (!string.IsNullOrWhiteSpace(request.ApplicationName))
        {
            // Look up application by name to get ID
            var applicationResult = await _applicationRepository.GetByNameAsync(request.ApplicationName, cancellationToken);
            if (applicationResult.IsFailure)
            {
                return Result.Success<IReadOnlyList<AuditLogDto>>(new List<AuditLogDto>());
            }

            var application = applicationResult.Value;

            // Get logs for this application and environment
            if (!string.IsNullOrWhiteSpace(request.Environment))
            {
                auditLogs = await _auditLogRepository.GetByApplicationAndEnvironmentAsync(
                    application.Id,
                    request.Environment,
                    request.FromDate,
                    request.ToDate,
                    cancellationToken);
            }
            else
            {
                auditLogs = await _auditLogRepository.GetByApplicationIdAsync(
                    application.Id,
                    request.PageSize,
                    request.PageNumber,
                    cancellationToken);
            }
        }
        else if (request.DecisionAllow == false)
        {
            // Get blocked decisions
            auditLogs = await _auditLogRepository.GetBlockedDecisionsAsync(
                request.FromDate,
                null,
                cancellationToken);
        }
        else
        {
            // Get logs with critical vulnerabilities as a reasonable default
            auditLogs = await _auditLogRepository.GetWithCriticalVulnerabilitiesAsync(
                request.FromDate,
                cancellationToken);
        }

        // Apply additional filters
        if (request.DecisionAllow.HasValue)
        {
            auditLogs = auditLogs.Where(a => a.Allowed == request.DecisionAllow.Value);
        }

        if (request.FromDate.HasValue)
        {
            auditLogs = auditLogs.Where(a => a.EvaluatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            auditLogs = auditLogs.Where(a => a.EvaluatedAt <= request.ToDate.Value);
        }

        // Apply pagination
        var skip = (request.PageNumber - 1) * request.PageSize;
        var pagedLogs = auditLogs
            .OrderByDescending(a => a.EvaluatedAt)
            .Skip(skip)
            .Take(request.PageSize)
            .ToList();

        var dtos = pagedLogs.Select(log =>
        {
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

            return new AuditLogDto
            {
                Id = log.Id,
                EvaluationId = evaluationGuid,
                ApplicationName = log.ApplicationName,
                Environment = log.Environment,
                Timestamp = log.EvaluatedAt,
                DecisionAllow = log.Allowed,
                Violations = log.Violations.ToList(),
                PolicyPackage = "compliance.default", // Not stored in domain, use default
                TotalVulnerabilities = log.TotalVulnerabilityCount,
                CriticalCount = log.CriticalCount,
                HighCount = log.HighCount,
                InitiatedBy = "system", // Not stored in domain, use default
                CompleteEvidence = completeEvidence
            };
        }).ToList();

        return Result.Success<IReadOnlyList<AuditLogDto>>(dtos);
    }
}
