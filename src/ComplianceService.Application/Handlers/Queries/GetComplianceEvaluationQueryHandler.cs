using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
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

    public GetComplianceEvaluationQueryHandler(IComplianceEvaluationRepository evaluationRepository)
    {
        _evaluationRepository = evaluationRepository;
    }

    public async Task<Result<ComplianceEvaluationDto>> Handle(GetComplianceEvaluationQuery request, CancellationToken cancellationToken)
    {
        var evaluation = await _evaluationRepository.GetByIdAsync(request.EvaluationId, cancellationToken);

        if (evaluation == null)
        {
            return Result.Failure<ComplianceEvaluationDto>($"Compliance evaluation with ID '{request.EvaluationId}' not found");
        }

        return Result.Success(new ComplianceEvaluationDto
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
        });
    }
}
