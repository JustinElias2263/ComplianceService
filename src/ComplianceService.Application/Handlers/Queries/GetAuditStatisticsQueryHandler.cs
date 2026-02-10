using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting audit statistics
/// </summary>
public class GetAuditStatisticsQueryHandler : IRequestHandler<GetAuditStatisticsQuery, Result<AuditStatisticsDto>>
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditStatisticsQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<AuditStatisticsDto>> Handle(GetAuditStatisticsQuery request, CancellationToken cancellationToken)
    {
        var statistics = await _auditLogRepository.GetStatisticsAsync(
            request.FromDate,
            request.ToDate,
            cancellationToken);

        var dto = new AuditStatisticsDto
        {
            TotalEvaluations = statistics.TotalEvaluations,
            AllowedCount = statistics.AllowedCount,
            BlockedCount = statistics.BlockedCount,
            BlockedPercentage = statistics.BlockedPercentage,
            TotalCriticalVulnerabilities = statistics.TotalCriticalVulnerabilities,
            TotalHighVulnerabilities = statistics.TotalHighVulnerabilities,
            EvaluationsByEnvironment = statistics.EvaluationsByEnvironment,
            EvaluationsByRiskTier = statistics.EvaluationsByRiskTier
        };

        return Result.Success(dto);
    }
}
