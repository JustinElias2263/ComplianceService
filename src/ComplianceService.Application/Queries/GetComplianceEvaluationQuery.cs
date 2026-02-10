using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get a compliance evaluation by ID
/// </summary>
public record GetComplianceEvaluationQuery : IRequest<Result<ComplianceEvaluationDto>>
{
    public required Guid EvaluationId { get; init; }
}
