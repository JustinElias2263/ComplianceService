using ComplianceService.Application.DTOs;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Commands;

/// <summary>
/// Command to update the owner of an application
/// </summary>
public record UpdateApplicationOwnerCommand : IRequest<Result<ApplicationDto>>
{
    public required Guid ApplicationId { get; init; }
    public required string NewOwner { get; init; }
}
