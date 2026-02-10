using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Evaluation.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting compliance evaluations for a specific application
/// </summary>
public class GetComplianceEvaluationsByApplicationQueryHandler : IRequestHandler<GetComplianceEvaluationsByApplicationQuery, Result<IReadOnlyList<ComplianceEvaluationDto>>>
{
    private readonly IComplianceEvaluationRepository _evaluationRepository;
    private readonly IApplicationRepository _applicationRepository;

    public GetComplianceEvaluationsByApplicationQueryHandler(
        IComplianceEvaluationRepository evaluationRepository,
        IApplicationRepository applicationRepository)
    {
        _evaluationRepository = evaluationRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<IReadOnlyList<ComplianceEvaluationDto>>> Handle(
        GetComplianceEvaluationsByApplicationQuery request,
        CancellationToken cancellationToken)
    {
        // Get application to retrieve name
        var applicationResult = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (applicationResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ComplianceEvaluationDto>>($"Application with ID {request.ApplicationId} not found");
        }

        var application = applicationResult.Value;

        // Get evaluations based on filter criteria
        IReadOnlyList<ComplianceService.Domain.Evaluation.ComplianceEvaluation> evaluations;

        if (!string.IsNullOrEmpty(request.Environment))
        {
            evaluations = await _evaluationRepository.GetByApplicationAndEnvironmentAsync(
                request.ApplicationId,
                request.Environment,
                cancellationToken);
        }
        else
        {
            evaluations = await _evaluationRepository.GetByApplicationIdAsync(
                request.ApplicationId,
                cancellationToken);
        }

        // Filter by date range
        var cutoffDate = DateTime.UtcNow.AddDays(-request.Days);
        evaluations = evaluations
            .Where(e => e.EvaluatedAt >= cutoffDate)
            .OrderByDescending(e => e.EvaluatedAt)
            .ToList();

        // Map to DTOs
        var dtos = evaluations.Select(evaluation =>
        {
            // Get policy package from environment config
            string policyPackage = "compliance.default";
            var environmentResult = application.GetEnvironment(evaluation.Environment);
            if (environmentResult.IsSuccess)
            {
                policyPackage = environmentResult.Value.Policies.FirstOrDefault()?.PackageName ?? "compliance.default";
            }

            return new ComplianceEvaluationDto
            {
                Id = evaluation.Id,
                ApplicationId = evaluation.ApplicationId,
                ApplicationName = application.Name,
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
