using ComplianceService.Application.Commands;
using FluentValidation;

namespace ComplianceService.Application.Validators;

/// <summary>
/// Validator for RegisterApplicationCommand
/// </summary>
public class RegisterApplicationCommandValidator : AbstractValidator<RegisterApplicationCommand>
{
    public RegisterApplicationCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Application name is required")
            .MaximumLength(100).WithMessage("Application name must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9-_.]+$").WithMessage("Application name can only contain letters, numbers, hyphens, underscores, and dots");

        RuleFor(x => x.RiskTier)
            .NotEmpty().WithMessage("Risk tier is required")
            .Must(BeValidRiskTier).WithMessage("Risk tier must be one of: critical, high, medium, low");

        RuleFor(x => x.Owner)
            .NotEmpty().WithMessage("Owner is required")
            .MaximumLength(200).WithMessage("Owner must not exceed 200 characters");
    }

    private bool BeValidRiskTier(string riskTier)
    {
        var validTiers = new[] { "critical", "high", "medium", "low" };
        return validTiers.Contains(riskTier?.ToLowerInvariant());
    }
}
