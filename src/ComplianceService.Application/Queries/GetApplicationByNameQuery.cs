using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Queries;

/// <summary>
/// Query to get an application by its name
/// </summary>
public record GetApplicationByNameQuery : IRequest<Result<ApplicationDto>>
{
    public required string Name { get; init; }
}
