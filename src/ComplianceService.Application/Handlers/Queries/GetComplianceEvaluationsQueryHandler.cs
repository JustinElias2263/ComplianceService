using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
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

    public GetComplianceEvaluationsQueryHandler(IComplianceEvaluationRepository evaluationRepository)
    {
        _evaluationRepository = evaluationRepository;
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
            evaluations = evaluations.Where(e => e.Passed == request.Passed.Value);
        }

        // Apply pagination
        var skip = (request.PageNumber - 1) * request.PageSize;
        var pagedEvaluations = evaluations
            .OrderByDescending(e => e.EvaluatedAt)
            .Skip(skip)
            .Take(request.PageSize)
            .ToList();

        var dtos = pagedEvaluations.Select(evaluation => new ComplianceEvaluationDto
        {
            Id = evaluation.Id,
            ApplicationId = evaluation.ApplicationId,
            ApplicationName = evaluation.ApplicationName,
            Environment = evaluation.Environment,
            EvaluatedAt = evaluation.EvaluatedAt,
            Passed = evaluation.Passed,
            ScanResults = evaluation.ScanResults.Select(sr => new ScanResultDto
            {
                ToolName = sr.ToolName,
                ScannedAt = sr.ScannedAt,
                Vulnerabilities = sr.Vulnerabilities.Select(v => new VulnerabilityDto
                {
                    CveId = v.CveId,
                    Severity = v.Severity.Value,
                    CvssScore = v.CvssScore,
                    PackageName = v.PackageName,
                    CurrentVersion = v.CurrentVersion,
                    FixedVersion = v.FixedVersion,
                    Description = v.Description,
                    IsFixable = v.IsFixable,
                    Source = sr.ToolName
                }).ToList(),
                RawOutput = sr.RawOutput
            }).ToList(),
            PolicyDecision = new PolicyDecisionDto
            {
                Allow = evaluation.PolicyDecision.Allow,
                Violations = evaluation.PolicyDecision.Violations.Select(v => new PolicyViolationDto
                {
                    Rule = "Policy Violation",
                    Message = v,
                    Severity = "high"
                }).ToList(),
                PolicyPackage = evaluation.PolicyDecision.PolicyPackage,
                Reason = evaluation.PolicyDecision.Allow ? "All policies passed" : "Policy violations detected"
            },
            AggregatedCounts = new VulnerabilityCountsDto
            {
                Critical = evaluation.AggregatedCounts.Critical,
                High = evaluation.AggregatedCounts.High,
                Medium = evaluation.AggregatedCounts.Medium,
                Low = evaluation.AggregatedCounts.Low,
                Total = evaluation.AggregatedCounts.Total
            }
        }).ToList();

        return Result.Success<IReadOnlyList<ComplianceEvaluationDto>>(dtos);
    }
}
