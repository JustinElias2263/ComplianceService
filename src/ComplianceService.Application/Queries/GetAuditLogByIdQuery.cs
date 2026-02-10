using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get an audit log by ID
/// </summary>
public record GetAuditLogByIdQuery : IRequest<Result<AuditLogDto>>
{
    public required Guid AuditLogId { get; init; }
}
