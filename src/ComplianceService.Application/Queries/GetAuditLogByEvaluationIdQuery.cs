using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get an audit log by evaluation ID
/// </summary>
public record GetAuditLogByEvaluationIdQuery : IRequest<Result<AuditLogDto>>
{
    public required string EvaluationId { get; init; }
}
