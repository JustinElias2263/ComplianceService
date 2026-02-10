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
        var applicationResult = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (applicationResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(applicationResult.Error);
        }

        var application = applicationResult.Value;

        var environmentResult = application.GetEnvironment(request.Environment);
        if (environmentResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(environmentResult.Error);
        }

        var environment = environmentResult.Value;

        if (!application.IsActive)
        {
            return Result.Failure<ComplianceEvaluationDto>($"Application '{application.Name}' is not active");
        }

        // 2. Convert scan results to domain value objects
        var scanResults = new List<ScanResult>();
        foreach (var scanDto in request.ScanResults)
        {
            var vulnerabilities = new List<Vulnerability>();
            foreach (var vulnDto in scanDto.Vulnerabilities)
            {
                var vulnResult = Vulnerability.Create(
                    vulnDto.CveId,
                    vulnDto.Description ?? vulnDto.CveId,
                    vulnDto.Severity,
                    vulnDto.CvssScore,
                    vulnDto.PackageName,
                    vulnDto.CurrentVersion,
                    vulnDto.FixedVersion);

                if (vulnResult.IsFailure)
                {
                    return Result.Failure<ComplianceEvaluationDto>(vulnResult.Error);
                }

                vulnerabilities.Add(vulnResult.Value);
            }

            var scanResult = ScanResult.Create(
                scanDto.ToolName,
                "1.0",
                scanDto.ScannedAt ?? DateTime.UtcNow,
                vulnerabilities);
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
        var policyPackage = environment.Policies.FirstOrDefault()?.PackageName
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
            opaDecision.Violations.Select(v => v.Message).ToList());

        if (policyDecisionResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(policyDecisionResult.Error);
        }

        // 5. Create compliance evaluation aggregate
        var evaluationResult = ComplianceEvaluation.Create(
            request.ApplicationId,
            request.Environment,
            environment.RiskTier.Value,
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
        var scanResultsJson = JsonSerializer.Serialize(request.ScanResults);
        var policyInputJson = JsonSerializer.Serialize(opaInput);
        var policyOutputJson = JsonSerializer.Serialize(opaDecision);

        var evidenceResult = DecisionEvidence.Create(scanResultsJson, policyInputJson, policyOutputJson);
        if (evidenceResult.IsFailure)
        {
            return Result.Failure<ComplianceEvaluationDto>(evidenceResult.Error);
        }

        var auditLogResult = AuditLog.Create(
            evaluation.Id.ToString(),
            application.Id,
            application.Name,
            evaluation.Environment,
            evaluation.RiskTier,
            policyDecisionResult.Value.Allowed,
            policyDecisionResult.Value.GetReason(),
            policyDecisionResult.Value.Violations,
            evidenceResult.Value,
            policyDecisionResult.Value.EvaluationDurationMs,
            evaluation.GetCriticalVulnerabilityCount(),
            evaluation.GetHighVulnerabilityCount(),
            evaluation.GetMediumVulnerabilityCount(),
            evaluation.GetLowVulnerabilityCount(),
            evaluation.EvaluatedAt);

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
                var criticalCount = evaluation.GetCriticalVulnerabilityCount();
                var highCount = evaluation.GetHighVulnerabilityCount();

                if (evaluation.IsBlocked || criticalCount > 0)
                {
                    var recipients = new List<string> { application.Owner };

                    if (evaluation.IsBlocked)
                    {
                        await _notificationService.SendComplianceNotificationAsync(
                            application.Name,
                            request.Environment,
                            evaluation.IsAllowed,
                            opaDecision.Violations.Select(v => v.Message).ToList(),
                            recipients,
                            CancellationToken.None);
                    }

                    if (criticalCount > 0 || highCount > 0)
                    {
                        await _notificationService.SendCriticalVulnerabilityAlertAsync(
                            application.Name,
                            request.Environment,
                            criticalCount,
                            highCount,
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
        return Result.Success(MapToDto(evaluation, application.Name, policyPackage));
    }

    private static ComplianceEvaluationDto MapToDto(ComplianceEvaluation evaluation, string applicationName, string policyPackage)
    {
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
    }
}
