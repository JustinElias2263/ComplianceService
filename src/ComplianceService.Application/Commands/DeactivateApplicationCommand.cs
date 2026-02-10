using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Commands;

/// <summary>
/// Command to deactivate an application
/// </summary>
public record DeactivateApplicationCommand : IRequest<Result>
{
    public required Guid ApplicationId { get; init; }
}
