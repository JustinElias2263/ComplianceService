using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using ComplianceService.Application.Interfaces;
using ComplianceService.Domain.ApplicationProfile.Interfaces;
using ComplianceService.Domain.Audit;
using ComplianceService.Domain.Audit.Interfaces;
using ComplianceService.Domain.Audit.ValueObjects;
using ComplianceService.Domain.Evaluation;
using ComplianceService.Domain.Evaluation.Interfaces;
using ComplianceService.Domain.Evaluation.ValueObjects;
using ComplianceService.Domain.Shared;
using MediatR;
using System.Text.Json;

namespace ComplianceService.Application.Handlers.Commands;

/// <summary>
/// Handler for evaluating compliance
/// This is the main CI/CD integration workflow
/// </summary>
public class EvaluateComplianceCommandHandler : IRequestHandler<EvaluateComplianceCommand, Result<ComplianceEvaluationDto>>
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IComplianceEvaluationRepository _evaluationRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IOpaClient _opaClient;
    private readonly INotificationService _notificationService;

    public EvaluateComplianceCommandHandler(
        IApplicationRepository applicationRepository,
        IComplianceEvaluationRepository evaluationRepository,
        IAuditLogRepository auditLogRepository,
        IOpaClient opaClient,
        INotificationService notificationService)
    {
        _applicationRepository = applicationRepository;
        _evaluationRepository = evaluationRepository;
        _auditLogRepository = auditLogRepository;
        _opaClient = opaClient;
        _notificationService = notificationService;
    }

    public async Task<Result<ComplianceEvaluationDto>> Handle(EvaluateComplianceCommand request, CancellationToken cancellationToken)
    {
        // 1. Get application and environment config
        var application = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application == null)
        {
            return Result.Failure<ComplianceEvaluationDto>($"Application with ID '{request.ApplicationId}' not found");
        }

        var environmentResult = application.GetEnvironment(request.Environment);
        if (environmentResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(environmentResult.Error);
        }

        var environment = environmentResult.Value;

        if (!environment.IsActive)
        {
            return Result.Failure<ComplianceEvaluationDto>($"Environment '{request.Environment}' is not active for application '{application.Name}'");
        }

        // 2. Convert scan results to domain value objects
        var scanResults = new List<ScanResult>();
        foreach (var scanDto in request.ScanResults)
        {
            var vulnerabilities = new List<Vulnerability>();
            foreach (var vulnDto in scanDto.Vulnerabilities)
            {
                var severityResult = VulnerabilitySeverity.FromString(vulnDto.Severity);
                if (severityResult.IsFailure)
                {
                    return Result.Failure<ComplianceEvaluationDto>(severityResult.Error);
                }

                var vulnResult = Vulnerability.Create(
                    vulnDto.CveId,
                    severityResult.Value,
                    vulnDto.CvssScore,
                    vulnDto.PackageName,
                    vulnDto.CurrentVersion,
                    vulnDto.FixedVersion,
                    vulnDto.Description,
                    vulnDto.IsFixable);

                if (vulnResult.IsFailure)
                {
                    return Result.Failure<ComplianceEvaluationDto>(vulnResult.Error);
                }

                vulnerabilities.Add(vulnResult.Value);
            }

            var scanResult = ScanResult.Create(scanDto.ToolName, scanDto.ScannedAt, vulnerabilities, scanDto.RawOutput);
            if (scanResult.IsFailure)
            {
                return Result.Failure<ComplianceEvaluationDto>(scanResult.Error);
            }

            scanResults.Add(scanResult.Value);
        }

        // 3. Call OPA for policy evaluation
        var opaInput = new OpaInputDto
        {
            Application = new ApplicationContextDto
            {
                Name = application.Name,
                Environment = request.Environment,
                RiskTier = environment.RiskTier.Value,
                Owner = application.Owner
            },
            ScanResults = request.ScanResults,
            Metadata = request.Metadata
        };

        // Use the first policy reference (in a real implementation, you might aggregate multiple policies)
        var policyPackage = environment.PolicyReferences.FirstOrDefault()?.PackageName
            ?? "compliance.default";

        OpaDecisionDto opaDecision;
        try
        {
            opaDecision = await _opaClient.EvaluatePolicyAsync(opaInput, policyPackage, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<ComplianceEvaluationDto>($"OPA policy evaluation failed: {ex.Message}");
        }

        // 4. Create policy decision value object
        var policyDecisionResult = PolicyDecision.Create(
            opaDecision.Allow,
            opaDecision.Violations.Select(v => v.Message).ToList(),
            policyPackage);

        if (policyDecisionResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(policyDecisionResult.Error);
        }

        // 5. Create compliance evaluation aggregate
        var evaluationResult = ComplianceEvaluation.Create(
            request.ApplicationId,
            application.Name,
            request.Environment,
            scanResults,
            policyDecisionResult.Value);

        if (evaluationResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(evaluationResult.Error);
        }

        var evaluation = evaluationResult.Value;

        // 6. Persist evaluation
        await _evaluationRepository.AddAsync(evaluation, cancellationToken);
        await _evaluationRepository.SaveChangesAsync(cancellationToken);

        // 7. Create audit log
        var completeEvidence = JsonSerializer.Serialize(new
        {
            Application = opaInput.Application,
            ScanResults = request.ScanResults,
            OpaDecision = opaDecision,
            Metadata = request.Metadata
        });

        var evidenceResult = DecisionEvidence.Create(completeEvidence);
        if (evidenceResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(evidenceResult.Error);
        }

        var auditLogResult = AuditLog.Create(
            evaluation.Id,
            application.Name,
            request.Environment,
            policyDecisionResult.Value,
            evaluation.AggregatedCounts.Total,
            evaluation.AggregatedCounts.Critical,
            evaluation.AggregatedCounts.High,
            request.InitiatedBy,
            evidenceResult.Value);

        if (auditLogResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(auditLogResult.Error);
        }

        await _auditLogRepository.AddAsync(auditLogResult.Value, cancellationToken);
        await _auditLogRepository.SaveChangesAsync(cancellationToken);

        // 8. Send notifications asynchronously (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                if (!evaluation.Passed || evaluation.AggregatedCounts.Critical > 0)
                {
                    var recipients = new List<string> { application.Owner };

                    if (!evaluation.Passed)
                    {
                        await _notificationService.SendComplianceNotificationAsync(
                            application.Name,
                            request.Environment,
                            evaluation.Passed,
                            opaDecision.Violations.Select(v => v.Message).ToList(),
                            recipients,
                            CancellationToken.None);
                    }

                    if (evaluation.AggregatedCounts.Critical > 0 || evaluation.AggregatedCounts.High > 0)
                    {
                        await _notificationService.SendCriticalVulnerabilityAlertAsync(
                            application.Name,
                            request.Environment,
                            evaluation.AggregatedCounts.Critical,
                            evaluation.AggregatedCounts.High,
                            recipients,
                            CancellationToken.None);
                    }
                }
            }
            catch
            {
                // Log but don't fail the evaluation
            }
        }, cancellationToken);

        // 9. Map to DTO and return
        return Result.Success(MapToDto(evaluation));
    }

    private static ComplianceEvaluationDto MapToDto(ComplianceEvaluation evaluation)
    {
        return new ComplianceEvaluationDto
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
        };
    }
}
