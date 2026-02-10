using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting audit logs with filtering and pagination
/// </summary>
public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<IReadOnlyList<AuditLogDto>>>
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditLogsQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<IReadOnlyList<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        // Build search query
        IEnumerable<Domain.Audit.AuditLog> auditLogs;

        if (!string.IsNullOrWhiteSpace(request.ApplicationName))
        {
            auditLogs = await _auditLogRepository.SearchAsync(
                request.ApplicationName,
                request.Environment,
                request.DecisionAllow,
                request.FromDate,
                request.ToDate,
                cancellationToken);
        }
        else
        {
            // Get all within date range
            auditLogs = await _auditLogRepository.GetByDateRangeAsync(
                request.FromDate ?? DateTime.UtcNow.AddDays(-30),
                request.ToDate ?? DateTime.UtcNow,
                cancellationToken);
        }

        // Apply pagination
        var skip = (request.PageNumber - 1) * request.PageSize;
        var pagedLogs = auditLogs
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(request.PageSize)
            .ToList();

        var dtos = pagedLogs.Select(log => new AuditLogDto
        {
            Id = log.Id,
            EvaluationId = log.EvaluationId,
            ApplicationName = log.ApplicationName,
            Environment = log.Environment,
            Timestamp = log.Timestamp,
            DecisionAllow = log.DecisionAllow,
            Violations = log.Violations.ToList(),
            PolicyPackage = log.PolicyPackage,
            TotalVulnerabilities = log.TotalVulnerabilities,
            CriticalCount = log.CriticalCount,
            HighCount = log.HighCount,
            InitiatedBy = log.InitiatedBy,
            CompleteEvidence = log.CompleteEvidence.JsonData
        }).ToList();

        return Result.Success<IReadOnlyList<AuditLogDto>>(dtos);
    }
}
