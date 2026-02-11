using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceService.Api.Controllers;

/// <summary>
/// Provides audit log queries and compliance reporting
/// </summary>
[ApiController]
[Route("api/audit")]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IMediator mediator, ILogger<AuditController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get audit log by ID
    /// </summary>
    /// <param name="id">Audit log ID</param>
    /// <returns>Audit log details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLogById(Guid id)
    {
        var query = new GetAuditLogByIdQuery { AuditLogId = id };
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Audit Log Not Found",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get audit log by evaluation ID
    /// </summary>
    /// <param name="evaluationId">Evaluation ID</param>
    /// <returns>Audit log details</returns>
    [HttpGet("evaluation/{evaluationId}")]
    [ProducesResponseType(typeof(AuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLogByEvaluationId(string evaluationId)
    {
        var query = new GetAuditLogByEvaluationIdQuery { EvaluationId = evaluationId };
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Audit Log Not Found",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get audit logs for an application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="environment">Optional environment filter</param>
    /// <param name="fromDate">Start date (optional)</param>
    /// <param name="toDate">End date (optional)</param>
    /// <param name="pageSize">Page size (default: 50)</param>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <returns>List of audit logs</returns>
    [HttpGet("application/{applicationId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogsByApplication(
        Guid applicationId,
        [FromQuery] string? environment = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int pageSize = 50,
        [FromQuery] int pageNumber = 1)
    {
        var query = new GetAuditLogsByApplicationQuery
        {
            ApplicationId = applicationId,
            Environment = environment,
            FromDate = fromDate,
            ToDate = toDate,
            PageSize = pageSize,
            PageNumber = pageNumber
        };

        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Audit Logs",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get blocked decisions (denied deployments)
    /// </summary>
    /// <param name="days">Number of days to look back</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of blocked decisions</returns>
    [HttpGet("blocked")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlockedDecisions(
        [FromQuery] int? days = null,
        [FromQuery] int? limit = null)
    {
        var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;

        var query = new GetBlockedDecisionsQuery
        {
            Since = since,
            Limit = limit
        };

        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Blocked Decisions",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get audit logs with critical vulnerabilities
    /// </summary>
    /// <param name="days">Number of days to look back</param>
    /// <returns>List of audit logs with critical vulnerabilities</returns>
    [HttpGet("critical-vulnerabilities")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCriticalVulnerabilities([FromQuery] int? days = null)
    {
        var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;

        var query = new GetCriticalVulnerabilitiesQuery { Since = since };
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Critical Vulnerabilities",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get audit logs by risk tier
    /// </summary>
    /// <param name="riskTier">Risk tier (critical, high, medium, low)</param>
    /// <param name="fromDate">Start date (optional)</param>
    /// <param name="toDate">End date (optional)</param>
    /// <returns>List of audit logs</returns>
    [HttpGet("risk-tier/{riskTier}")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogsByRiskTier(
        string riskTier,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = new GetAuditLogsByRiskTierQuery
        {
            RiskTier = riskTier,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Audit Logs",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get audit statistics
    /// </summary>
    /// <param name="fromDate">Start date (optional)</param>
    /// <param name="toDate">End date (optional)</param>
    /// <returns>Audit statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(AuditStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditStatistics(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = new GetAuditStatisticsQuery
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Statistics",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }
}
