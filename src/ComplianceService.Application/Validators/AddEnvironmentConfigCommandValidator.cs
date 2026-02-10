using ComplianceService.Application.Commands;
using FluentValidation;

namespace ComplianceService.Application.Validators;

/// <summary>
/// Validator for AddEnvironmentConfigCommand
/// </summary>
public class AddEnvironmentConfigCommandValidator : AbstractValidator<AddEnvironmentConfigCommand>
{
    public AddEnvironmentConfigCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty().WithMessage("Application ID is required");

        RuleFor(x => x.EnvironmentName)
            .NotEmpty().WithMessage("Environment name is required")
            .MaximumLength(50).WithMessage("Environment name must not exceed 50 characters")
            .Matches("^[a-z0-9-]+$").WithMessage("Environment name must be lowercase and can only contain letters, numbers, and hyphens");

        RuleFor(x => x.RiskTier)
            .NotEmpty().WithMessage("Risk tier is required")
            .Must(BeValidRiskTier).WithMessage("Risk tier must be one of: critical, high, medium, low");

        RuleFor(x => x.SecurityTools)
            .NotEmpty().WithMessage("At least one security tool must be specified")
            .Must(tools => tools != null && tools.Count > 0).WithMessage("At least one security tool must be specified")
            .ForEach(tool =>
            {
                tool.Must(BeValidSecurityTool).WithMessage("Security tool must be one of: snyk, prismacloud");
            });

        RuleFor(x => x.PolicyReferences)
            .NotEmpty().WithMessage("At least one policy reference must be specified")
            .Must(policies => policies != null && policies.Count > 0).WithMessage("At least one policy reference must be specified")
            .ForEach(policy =>
            {
                policy.NotEmpty().WithMessage("Policy reference cannot be empty")
                      .MaximumLength(200).WithMessage("Policy reference must not exceed 200 characters");
            });
    }

    private bool BeValidRiskTier(string riskTier)
    {
        var validTiers = new[] { "critical", "high", "medium", "low" };
        return validTiers.Contains(riskTier?.ToLowerInvariant());
    }

    private bool BeValidSecurityTool(string tool)
    {
        var validTools = new[] { "snyk", "prismacloud" };
        return validTools.Contains(tool?.ToLowerInvariant());
    }
}
