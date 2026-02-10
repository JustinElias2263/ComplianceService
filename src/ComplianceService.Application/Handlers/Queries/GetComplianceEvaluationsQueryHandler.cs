using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Evaluation.Interfaces;
using ComplianceService.Domain.Shared;
using MediatR;

namespace ComplianceService.Application.Handlers.Queries;

/// <summary>
/// Handler for getting compliance evaluations with filtering and pagination
/// </summary>
public class GetComplianceEvaluationsQueryHandler : IRequestHandler<GetComplianceEvaluationsQuery, Result<IReadOnlyList<ComplianceEvaluationDto>>>
{
    private readonly IComplianceEvaluationRepository _evaluationRepository;
    private readonly IApplicationRepository _applicationRepository;

    public GetComplianceEvaluationsQueryHandler(
        IComplianceEvaluationRepository evaluationRepository,
        IApplicationRepository applicationRepository)
    {
        _evaluationRepository = evaluationRepository;
        _applicationRepository = applicationRepository;
    }

    public async Task<Result<IReadOnlyList<ComplianceEvaluationDto>>> Handle(GetComplianceEvaluationsQuery request, CancellationToken cancellationToken)
    {
        // Get evaluations based on filters
        IEnumerable<Domain.Evaluation.ComplianceEvaluation> evaluations;

        if (request.ApplicationId.HasValue)
        {
            evaluations = await _evaluationRepository.GetByApplicationIdAsync(
                request.ApplicationId.Value,
                request.FromDate,
                request.ToDate,
                cancellationToken);
        }
        else
        {
            // Would typically get all, but for now return empty
            evaluations = new List<Domain.Evaluation.ComplianceEvaluation>();
        }

        // Apply additional filters
        if (!string.IsNullOrWhiteSpace(request.Environment))
        {
            evaluations = evaluations.Where(e =>
                e.Environment.Equals(request.Environment, StringComparison.OrdinalIgnoreCase));
        }

        if (request.Passed.HasValue)
        {
            evaluations = evaluations.Where(e => e.IsAllowed == request.Passed.Value);
        }

        // Apply pagination
        var skip = (request.PageNumber - 1) * request.PageSize;
        var pagedEvaluations = evaluations
            .OrderByDescending(e => e.EvaluatedAt)
            .Skip(skip)
            .Take(request.PageSize)
            .ToList();

        var dtos = new List<ComplianceEvaluationDto>();
        foreach (var evaluation in pagedEvaluations)
        {
            // Get application to retrieve name
            var application = await _applicationRepository.GetByIdAsync(evaluation.ApplicationId, cancellationToken);
            var applicationName = application?.Name ?? "Unknown";

            // Get policy package from environment config
            var environmentResult = application?.GetEnvironment(evaluation.Environment);
            var policyPackage = environmentResult?.IsSuccess == true
                ? environmentResult.Value.Policies.FirstOrDefault()?.PackageName ?? "compliance.default"
                : "compliance.default";

            dtos.Add(new ComplianceEvaluationDto
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

        return Result.Success<IReadOnlyList<ComplianceEvaluationDto>>(dtos);
    }
}
