using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Evaluation.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting a compliance evaluation by ID
/// </summary>
public class GetComplianceEvaluationQueryHandler : IRequestHandler<GetComplianceEvaluationQuery, Result<ComplianceEvaluationDto>>
{
    private readonly IComplianceEvaluationRepository _evaluationRepository;
    private readonly IApplicationRepository _applicationRepository;

    public GetComplianceEvaluationQueryHandler(
        IComplianceEvaluationRepository evaluationRepository,
        IApplicationRepository applicationRepository)
    {
        _evaluationRepository = evaluationRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<ComplianceEvaluationDto>> Handle(GetComplianceEvaluationQuery request, CancellationToken cancellationToken)
    {
        var evaluationResult = await _evaluationRepository.GetByIdAsync(request.EvaluationId, cancellationToken);

        if (evaluationResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(evaluationResult.Error);
        }

        var evaluation = evaluationResult.Value;

        // Get application to retrieve name
        var application = await _applicationRepository.GetByIdAsync(evaluation.ApplicationId, cancellationToken);
        var applicationName = application?.Name ?? "Unknown";

        // Get policy package from environment config
        var environmentResult = application?.GetEnvironment(evaluation.Environment);
        var policyPackage = environmentResult?.IsSuccess == true
            ? environmentResult.Value.Policies.FirstOrDefault()?.PackageName ?? "compliance.default"
            : "compliance.default";

        return Result.Success(new ComplianceEvaluationDto
        {
            Id = evaluation.Id,
            ApplicationId = evaluation.ApplicationId,
            ApplicationName = applicationName,
            Environment = evaluation.Environment,
            EvaluatedAt = evaluation.EvaluatedAt,
            Passed = evaluation.IsAllowed,
            ScanResults = evaluation.ScanResults.Select(sr => new ScanResultDto
            {
                ToolName = sr.Tool,
                ScannedAt = sr.ScanDate,
                Vulnerabilities = sr.Vulnerabilities.Select(v => new VulnerabilityDto
                {
                    CveId = v.Id,
                    Severity = v.Severity.Value,
                    CvssScore = (decimal)v.CvssScore,
                    PackageName = v.PackageName,
                    CurrentVersion = v.PackageVersion,
                    FixedVersion = v.FixedIn,
                    Description = v.Title,
                    IsFixable = !string.IsNullOrEmpty(v.FixedIn),
                    Source = sr.Tool
                }).ToList(),
                RawOutput = string.Empty
            }).ToList(),
            PolicyDecision = new PolicyDecisionDto
            {
                Allow = evaluation.Decision.Allowed,
                Violations = evaluation.Decision.Violations.Select(v => new PolicyViolationDto
                {
                    Rule = "Policy Violation",
                    Message = v,
                    Severity = "high"
                }).ToList(),
                PolicyPackage = policyPackage,
                Reason = evaluation.Decision.GetReason()
            },
            AggregatedCounts = new VulnerabilityCountsDto
            {
                Critical = evaluation.GetCriticalVulnerabilityCount(),
                High = evaluation.GetHighVulnerabilityCount(),
                Medium = evaluation.GetMediumVulnerabilityCount(),
                Low = evaluation.GetLowVulnerabilityCount(),
                Total = evaluation.GetTotalVulnerabilityCount()
            }
        });
    }
}
