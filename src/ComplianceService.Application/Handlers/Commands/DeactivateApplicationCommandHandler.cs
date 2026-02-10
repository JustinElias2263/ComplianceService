using ComplianceService.Application.Commands;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Commands;

/// <summary>
/// Handler for deactivating an application
/// </summary>
public class DeactivateApplicationCommandHandler : IRequestHandler<DeactivateApplicationCommand, Result>
{
    private readonly IApplicationRepository _applicationRepository;

    public DeactivateApplicationCommandHandler(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Result> Handle(DeactivateApplicationCommand request, CancellationToken cancellationToken)
    {
        // Get application
        var applicationResult = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (applicationResult.IsFailure)
        {
            return Result.Failure(applicationResult.Error);
        }

        var application = applicationResult.Value;

        // Deactivate
        application.Deactivate();

        // Persist
        await _applicationRepository.UpdateAsync(application, cancellationToken);
        await _applicationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
