using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceService.Api.Controllers;

/// <summary>
/// Handles compliance evaluation requests and queries
/// </summary>
[ApiController]
[Route("api/compliance")]
[Produces("application/json")]
public class ComplianceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(IMediator mediator, ILogger<ComplianceController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate compliance for an application deployment
    /// </summary>
    /// <param name="command">Compliance evaluation request</param>
    /// <returns>Compliance decision with allow/deny and violations</returns>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ComplianceEvaluationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EvaluateCompliance([FromBody] EvaluateComplianceCommand command)
    {
        _logger.LogInformation(
            "Evaluating compliance for application {ApplicationId} in environment {Environment}",
            command.ApplicationId,
            command.Environment);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Compliance evaluation failed for application {ApplicationId}: {Error}",
                command.ApplicationId,
                result.Error);

            return BadRequest(new ProblemDetails
            {
                Title = "Compliance Evaluation Failed",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        var evaluation = result.Value;

        if (evaluation.PolicyDecision.Allow)
        {
            _logger.LogInformation(
                "Compliance evaluation passed for application {ApplicationId} in {Environment}",
                command.ApplicationId,
                command.Environment);
        }
        else
        {
            _logger.LogWarning(
                "Compliance evaluation BLOCKED for application {ApplicationId} in {Environment}. Violations: {ViolationCount}",
                command.ApplicationId,
                command.Environment,
                evaluation.PolicyDecision.Violations.Count);
        }

        return Ok(evaluation);
    }

    /// <summary>
    /// Get evaluation by ID
    /// </summary>
    /// <param name="id">Evaluation ID</param>
    /// <returns>Evaluation details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ComplianceEvaluationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvaluationById(Guid id)
    {
        var query = new GetComplianceEvaluationByIdQuery { EvaluationId = id };
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Evaluation Not Found",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get evaluations for an application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="environment">Optional environment filter</param>
    /// <param name="days">Number of days to look back (default: 7)</param>
    /// <returns>List of evaluations</returns>
    [HttpGet("application/{applicationId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ComplianceEvaluationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvaluationsByApplication(
        Guid applicationId,
        [FromQuery] string? environment = null,
        [FromQuery] int days = 7)
    {
        var query = new GetComplianceEvaluationsByApplicationQuery
        {
            ApplicationId = applicationId,
            Environment = environment,
            Days = days
        };

        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Evaluations",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get recent evaluations across all applications
    /// </summary>
    /// <param name="days">Number of days to look back (default: 7)</param>
    /// <returns>List of recent evaluations</returns>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<ComplianceEvaluationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentEvaluations([FromQuery] int days = 7)
    {
        var query = new GetRecentEvaluationsQuery { Days = days };
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Recent Evaluations",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get blocked evaluations (denied deployments)
    /// </summary>
    /// <param name="days">Number of days to look back</param>
    /// <returns>List of blocked evaluations</returns>
    [HttpGet("blocked")]
    [ProducesResponseType(typeof(IReadOnlyList<ComplianceEvaluationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlockedEvaluations([FromQuery] int? days = null)
    {
        var since = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;

        var query = new GetBlockedEvaluationsQuery { Since = since };
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Blocked Evaluations",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }
}
