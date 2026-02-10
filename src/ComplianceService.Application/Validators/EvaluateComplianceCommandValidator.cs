using ComplianceService.Application.Commands;
using FluentValidation;

namespace ComplianceService.Application.Validators;

/// <summary>
/// Validator for EvaluateComplianceCommand
/// </summary>
public class EvaluateComplianceCommandValidator : AbstractValidator<EvaluateComplianceCommand>
{
    public EvaluateComplianceCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty().WithMessage("Application ID is required");

        RuleFor(x => x.Environment)
            .NotEmpty().WithMessage("Environment is required")
            .MaximumLength(50).WithMessage("Environment must not exceed 50 characters");

        RuleFor(x => x.ScanResults)
            .NotEmpty().WithMessage("At least one scan result must be provided")
            .Must(results => results != null && results.Count > 0).WithMessage("At least one scan result must be provided");

        RuleFor(x => x.InitiatedBy)
            .NotEmpty().WithMessage("InitiatedBy is required")
            .MaximumLength(200).WithMessage("InitiatedBy must not exceed 200 characters");

        RuleForEach(x => x.ScanResults).ChildRules(scanResult =>
        {
            scanResult.RuleFor(sr => sr.ToolName)
                .NotEmpty().WithMessage("Tool name is required");

            scanResult.RuleFor(sr => sr.ScannedAt)
                .NotEmpty().WithMessage("Scanned at timestamp is required")
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Scanned at cannot be in the future");

            scanResult.RuleFor(sr => sr.Vulnerabilities)
                .NotNull().WithMessage("Vulnerabilities list is required");

            scanResult.RuleFor(sr => sr.RawOutput)
                .NotEmpty().WithMessage("Raw output is required");
        });
    }
}
