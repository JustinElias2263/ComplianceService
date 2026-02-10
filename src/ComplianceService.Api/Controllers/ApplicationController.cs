using ComplianceService.Application.Commands;
using ComplianceService.Application.DTOs;
using ComplianceService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceService.Api.Controllers;

/// <summary>
/// Manages application registration and environment configuration
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ApplicationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ApplicationController> _logger;

    public ApplicationController(IMediator mediator, ILogger<ApplicationController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Register a new application
    /// </summary>
    /// <param name="command">Application registration details</param>
    /// <returns>Created application</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterApplication([FromBody] RegisterApplicationCommand command)
    {
        _logger.LogInformation("Registering application: {ApplicationName}", command.Name);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to register application {ApplicationName}: {Error}",
                command.Name, result.Error);
            return BadRequest(new ProblemDetails
            {
                Title = "Application Registration Failed",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Application {ApplicationName} registered successfully with ID {ApplicationId}",
            result.Value.Name, result.Value.Id);

        return CreatedAtAction(
            nameof(GetApplicationById),
            new { id = result.Value.Id },
            result.Value);
    }

    /// <summary>
    /// Get application by ID
    /// </summary>
    /// <param name="id">Application ID</param>
    /// <returns>Application details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplicationById(Guid id)
    {
        var query = new GetApplicationByIdQuery { ApplicationId = id };
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Application Not Found",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get application by name
    /// </summary>
    /// <param name="name">Application name</param>
    /// <returns>Application details</returns>
    [HttpGet("by-name/{name}")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplicationByName(string name)
    {
        var query = new GetApplicationByNameQuery { ApplicationName = name };
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Application Not Found",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get all applications
    /// </summary>
    /// <param name="owner">Optional filter by owner</param>
    /// <param name="activeOnly">Filter active applications only</param>
    /// <returns>List of applications</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllApplications(
        [FromQuery] string? owner = null,
        [FromQuery] bool activeOnly = false)
    {
        var query = new GetAllApplicationsQuery
        {
            Owner = owner,
            ActiveOnly = activeOnly
        };

        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Retrieve Applications",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Update application owner
    /// </summary>
    /// <param name="id">Application ID</param>
    /// <param name="command">Update command</param>
    /// <returns>Updated application</returns>
    [HttpPatch("{id:guid}/owner")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateApplicationOwner(
        Guid id,
        [FromBody] UpdateApplicationOwnerCommand command)
    {
        if (id != command.ApplicationId)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ID Mismatch",
                Detail = "Application ID in URL does not match command",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Update Failed",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Deactivate an application
    /// </summary>
    /// <param name="id">Application ID</param>
    /// <returns>No content</returns>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateApplication(Guid id)
    {
        var command = new DeactivateApplicationCommand { ApplicationId = id };
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Deactivation Failed",
                Detail = result.Error,
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Add environment configuration to an application
    /// </summary>
    /// <param name="id">Application ID</param>
    /// <param name="command">Environment configuration</param>
    /// <returns>Updated application</returns>
    [HttpPost("{id:guid}/environments")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddEnvironmentConfig(
        Guid id,
        [FromBody] AddEnvironmentConfigCommand command)
    {
        if (id != command.ApplicationId)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ID Mismatch",
                Detail = "Application ID in URL does not match command",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Add Environment",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Update environment configuration
    /// </summary>
    /// <param name="id">Application ID</param>
    /// <param name="environmentName">Environment name</param>
    /// <param name="command">Updated configuration</param>
    /// <returns>Updated application</returns>
    [HttpPut("{id:guid}/environments/{environmentName}")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateEnvironmentConfig(
        Guid id,
        string environmentName,
        [FromBody] UpdateEnvironmentConfigCommand command)
    {
        if (id != command.ApplicationId || environmentName != command.EnvironmentName)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ID Mismatch",
                Detail = "Application ID or environment name in URL does not match command",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Update Environment",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Deactivate an environment configuration
    /// </summary>
    /// <param name="id">Application ID</param>
    /// <param name="environmentName">Environment name</param>
    /// <returns>Updated application</returns>
    [HttpPost("{id:guid}/environments/{environmentName}/deactivate")]
    [ProducesResponseType(typeof(ApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateEnvironment(Guid id, string environmentName)
    {
        var command = new DeactivateEnvironmentCommand
        {
            ApplicationId = id,
            EnvironmentName = environmentName
        };

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to Deactivate Environment",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Value);
    }
}
