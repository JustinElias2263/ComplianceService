using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Evaluation.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting blocked compliance evaluations (denied deployments)
/// </summary>
public class GetBlockedEvaluationsQueryHandler : IRequestHandler<GetBlockedEvaluationsQuery, Result<IReadOnlyList<ComplianceEvaluationDto>>>
{
    private readonly IComplianceEvaluationRepository _evaluationRepository;
    private readonly IApplicationRepository _applicationRepository;

    public GetBlockedEvaluationsQueryHandler(
        IComplianceEvaluationRepository evaluationRepository,
        IApplicationRepository applicationRepository)
    {
        _evaluationRepository = evaluationRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<IReadOnlyList<ComplianceEvaluationDto>>> Handle(
        GetBlockedEvaluationsQuery request,
        CancellationToken cancellationToken)
    {
        var evaluations = await _evaluationRepository.GetBlockedEvaluationsAsync(request.Since, cancellationToken);

        // Get all applications for mapping
        var applications = await _applicationRepository.GetAllAsync(cancellationToken);
        var applicationDict = applications.ToDictionary(a => a.Id, a => a);

        // Map to DTOs
        var dtos = evaluations.Select(evaluation =>
        {
            var application = applicationDict.GetValueOrDefault(evaluation.ApplicationId);
            var applicationName = application?.Name ?? "Unknown";

            // Get policy package from environment config
            string policyPackage = "compliance.default";
            if (application != null)
            {
                var environmentResult = application.GetEnvironment(evaluation.Environment);
                if (environmentResult.IsSuccess)
                {
                    policyPackage = environmentResult.Value.Policies.FirstOrDefault()?.PackageName ?? "compliance.default";
                }
            }

            return new ComplianceEvaluationDto
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
            };
        }).ToList();

        return Result.Success<IReadOnlyList<ComplianceEvaluationDto>>(dtos);
    }
}
