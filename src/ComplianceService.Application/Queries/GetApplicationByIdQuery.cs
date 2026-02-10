using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get an application by its ID
/// </summary>
public record GetApplicationByIdQuery : IRequest<Result<ApplicationDto>>
{
    public required Guid ApplicationId { get; init; }
}
