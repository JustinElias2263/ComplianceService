# ComplianceService.Application Layer

## Table of Contents
- [Purpose](#purpose)
- [Clean Architecture Position](#clean-architecture-position)
- [CQRS Pattern](#cqrs-pattern)
- [Layer Components](#layer-components)
- [Development Guidelines](#development-guidelines)
- [Testing Strategy](#testing-strategy)
- [Scalability Patterns](#scalability-patterns)
- [Common Patterns](#common-patterns)

---

## Purpose

The **Application Layer** is the use case orchestration layer in Clean Architecture. It sits between the Domain and Infrastructure layers, coordinating application workflows without containing business logic.

**Core Responsibilities:**
- **Use Case Orchestration**: Coordinate domain objects to fulfill business use cases
- **Transaction Boundaries**: Define transaction scopes for data persistence
- **External Service Coordination**: Orchestrate calls to external services (OPA, notifications)
- **Data Transformation**: Map between Domain models and DTOs for API contracts
- **Input Validation**: Validate commands and queries before processing
- **Cross-Cutting Concerns**: Handle logging, authorization, and error handling

**What This Layer Does NOT Do:**
- ❌ Contain business rules (Domain layer responsibility)
- ❌ Know about databases or HTTP (Infrastructure layer responsibility)
- ❌ Handle presentation concerns (API layer responsibility)

---

## Clean Architecture Position

```
┌─────────────────────────────────────────────────────┐
│                    API Layer                        │
│              (Controllers, Middleware)              │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│              Application Layer (YOU ARE HERE)       │
│   ┌──────────────┐  ┌──────────────┐  ┌──────────┐ │
│   │   Commands   │  │   Queries    │  │   DTOs   │ │
│   └──────┬───────┘  └──────┬───────┘  └──────────┘ │
│          │                 │                        │
│   ┌──────▼─────────────────▼───────┐               │
│   │      Command/Query Handlers    │               │
│   └──────┬─────────────────────────┘               │
└──────────┼─────────────────────────────────────────┘
           │
┌──────────▼─────────────────────────────────────────┐
│                 Domain Layer                        │
│   (Aggregates, Entities, Value Objects, Events)    │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│              Infrastructure Layer                   │
│   (Repositories, OPA Client, Notifications, DB)     │
└─────────────────────────────────────────────────────┘
```

**Dependency Rules:**
- ✅ Application depends on Domain (core business logic)
- ✅ Application defines interfaces (implemented by Infrastructure)
- ❌ Application does NOT depend on Infrastructure
- ❌ Application does NOT depend on API/Presentation

---

## CQRS Pattern

This layer implements **Command Query Responsibility Segregation (CQRS)** using **MediatR**.

### Commands (Write Operations)
Commands change system state and return `Result<DTO>`.

**Available Commands:**
- `RegisterApplicationCommand` - Register new application profile
- `AddEnvironmentConfigCommand` - Add environment to application (with risk tier)
- `UpdateEnvironmentConfigCommand` - Update environment configuration (including risk tier)
- `EvaluateComplianceCommand` - **Main workflow** - evaluate compliance for deployment

**Command Flow:**
```
API Controller
   │
   ├─> Command (data)
   │
   ├─> Validator (FluentValidation)
   │
   ├─> Command Handler
   │     │
   │     ├─> Load Domain Aggregate
   │     ├─> Execute Business Logic
   │     ├─> Call External Services (OPA, Notifications)
   │     ├─> Persist via Repository
   │     └─> Return Result<DTO>
   │
   └─> Response (DTO or Error)
```

### Queries (Read Operations)
Queries retrieve data without side effects and return `Result<DTO>` or `Result<List<DTO>>`.

**Available Queries:**
- `GetApplicationByIdQuery` - Get single application
- `GetApplicationByNameQuery` - Get application by name
- `GetAllApplicationsQuery` - Get all applications (paginated, filtered)
- `GetComplianceEvaluationQuery` - Get single evaluation
- `GetComplianceEvaluationsQuery` - Get multiple evaluations (paginated, filtered)
- `GetAuditLogsQuery` - Get audit logs (paginated, filtered)

**Query Flow:**
```
API Controller
   │
   ├─> Query (filters)
   │
   ├─> Query Handler
   │     │
   │     ├─> Load from Repository
   │     ├─> Apply Filters
   │     ├─> Apply Pagination
   │     └─> Map to DTOs
   │
   └─> Response (DTO or Error)
```

---

## Layer Components

### 1. Commands (`/Commands`)

Commands represent **intentions to change system state**. They are immutable records.

**Example:**
```csharp
public record RegisterApplicationCommand : IRequest<Result<ApplicationDto>>
{
    public required string Name { get; init; }
    public required string Owner { get; init; }
}
```

**Design Guidelines:**
- Use `record` types (immutable by default)
- Use `required` properties for mandatory fields
- Return `Result<TDto>` (never throw exceptions for business failures)
- Implement `IRequest<Result<T>>` from MediatR

### 2. Queries (`/Queries`)

Queries represent **requests for data**. They support filtering and pagination.

**Example:**
```csharp
public record GetAllApplicationsQuery : IRequest<Result<IReadOnlyList<ApplicationDto>>>
{
    public string? Owner { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
```

**Design Guidelines:**
- Use nullable properties for optional filters
- Always include pagination (PageNumber, PageSize)
- Default PageSize to 50 (prevent unbounded queries)
- Return collections as `IReadOnlyList<T>`

### 3. Command Handlers (`/Handlers/Commands`)

Handlers orchestrate domain operations to fulfill commands.

**Example: EvaluateComplianceCommandHandler**
```csharp
public class EvaluateComplianceCommandHandler
    : IRequestHandler<EvaluateComplianceCommand, Result<ComplianceEvaluationDto>>
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IComplianceEvaluationRepository _evaluationRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IOpaClient _opaClient;
    private readonly INotificationService _notificationService;

    public async Task<Result<ComplianceEvaluationDto>> Handle(
        EvaluateComplianceCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Load application aggregate
        var application = await _applicationRepository.GetByIdAsync(
            request.ApplicationId, cancellationToken);

        if (application == null)
            return Result.Failure<ComplianceEvaluationDto>("Application not found");

        // 2. Get environment configuration
        var environment = application.GetEnvironment(request.Environment);
        if (environment == null)
            return Result.Failure<ComplianceEvaluationDto>("Environment not found");

        // 3. Convert DTOs to domain value objects
        var scanResults = MapScanResults(request.ScanResults);

        // 4. Call OPA for policy evaluation
        var opaDecision = await _opaClient.EvaluatePolicyAsync(
            BuildOpaInput(application, request),
            environment.PolicyReferences.First().PackageName,
            cancellationToken);

        // 5. Create evaluation aggregate
        var evaluationResult = ComplianceEvaluation.Create(
            request.ApplicationId,
            application.Name,
            request.Environment,
            scanResults,
            policyDecision);

        // 6. Persist evaluation
        await _evaluationRepository.AddAsync(evaluation, cancellationToken);
        await _evaluationRepository.SaveChangesAsync(cancellationToken);

        // 7. Create audit log
        var auditLog = AuditLog.Create(/* ... */);
        await _auditLogRepository.AddAsync(auditLog, cancellationToken);

        // 8. Send notifications (fire-and-forget)
        _ = Task.Run(() => SendNotifications(evaluation), cancellationToken);

        // 9. Return DTO
        return Result.Success(MapToDto(evaluation));
    }
}
```

**Handler Responsibilities:**
1. **Load aggregates** from repositories
2. **Execute domain logic** (via aggregate methods)
3. **Coordinate external services** (OPA, notifications)
4. **Handle transactions** (via repository.SaveChangesAsync)
5. **Map results to DTOs**
6. **Return Result<T>** (success or failure)

**Handler Guidelines:**
- ✅ Keep handlers thin (orchestration, not logic)
- ✅ Use domain aggregates for business rules
- ✅ Call SaveChangesAsync only after all validations pass
- ✅ Fire-and-forget for non-critical operations (notifications)
- ❌ Never throw exceptions for business failures
- ❌ Never put business logic in handlers

### 4. Query Handlers (`/Handlers/Queries`)

Query handlers retrieve and transform data.

**Example:**
```csharp
public class GetAllApplicationsQueryHandler
    : IRequestHandler<GetAllApplicationsQuery, Result<IReadOnlyList<ApplicationDto>>>
{
    private readonly IApplicationRepository _applicationRepository;

    public async Task<Result<IReadOnlyList<ApplicationDto>>> Handle(
        GetAllApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load all applications
        var applications = await _applicationRepository.GetAllAsync(cancellationToken);

        // 2. Apply filters
        if (!string.IsNullOrWhiteSpace(request.Owner))
            applications = applications.Where(a =>
                a.Owner.Equals(request.Owner, StringComparison.OrdinalIgnoreCase));

        // Note: RiskTier is now per-environment, not per-application

        // 3. Apply pagination
        var skip = (request.PageNumber - 1) * request.PageSize;
        var pagedApplications = applications.Skip(skip).Take(request.PageSize);

        // 4. Map to DTOs
        var dtos = pagedApplications.Select(MapToDto).ToList();

        return Result.Success<IReadOnlyList<ApplicationDto>>(dtos);
    }
}
```

**Query Handler Guidelines:**
- ✅ Simple data retrieval and mapping
- ✅ Apply filters in application code (or use repository methods)
- ✅ Always apply pagination limits
- ❌ No side effects (no state changes)
- ❌ No domain logic (pure data transformation)

### 5. DTOs (Data Transfer Objects) (`/DTOs`)

DTOs are contracts for API communication. They decouple domain models from external representation.

**Categories:**

**Application DTOs:**
- `ApplicationDto` - Application profile
- `EnvironmentConfigDto` - Environment configuration

**Evaluation DTOs:**
- `ComplianceEvaluationDto` - Evaluation result
- `ScanResultDto` - Security tool scan output
- `VulnerabilityDto` - Individual vulnerability
- `VulnerabilityCountsDto` - Aggregated counts
- `PolicyDecisionDto` - OPA decision summary

**OPA Integration DTOs:**
- `OpaInputDto` - Input to OPA policy engine
- `OpaDecisionDto` - Output from OPA
- `PolicyViolationDto` - Individual violation
- `ApplicationContextDto` - Application metadata for OPA

**Audit DTOs:**
- `AuditLogDto` - Audit trail entry

**DTO Design Guidelines:**
```csharp
public class ComplianceEvaluationDto
{
    // Use 'required' for mandatory fields
    public required Guid Id { get; init; }
    public required string ApplicationName { get; init; }

    // Use nullable for optional fields
    public string? Reason { get; init; }

    // Use init-only properties (immutable)
    public required DateTime EvaluatedAt { get; init; }

    // Use IReadOnlyList for collections
    public required IReadOnlyList<ScanResultDto> ScanResults { get; init; }
}
```

**DTO Guidelines:**
- ✅ Use `init` (immutable after construction)
- ✅ Use `required` for mandatory fields
- ✅ Use `IReadOnlyList<T>` for collections
- ✅ Flat structure (avoid deep nesting)
- ❌ No behavior (pure data)
- ❌ No validation logic (use validators)

### 6. Validators (`/Validators`)

Validators use **FluentValidation** to validate commands/queries before handler execution.

**Example (RegisterApplicationCommand):**
```csharp
public class RegisterApplicationCommandValidator : AbstractValidator<RegisterApplicationCommand>
{
    public RegisterApplicationCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Application name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9-_.]+$")
                .WithMessage("Name can only contain letters, numbers, hyphens, underscores, dots");

        RuleFor(x => x.Owner)
            .NotEmpty().WithMessage("Owner is required")
            .MaximumLength(200).WithMessage("Owner must not exceed 200 characters");
    }
}
```

**Example (AddEnvironmentConfigCommand - with RiskTier):**
```csharp
public class AddEnvironmentConfigCommandValidator : AbstractValidator<AddEnvironmentConfigCommand>
{
    public AddEnvironmentConfigCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty().WithMessage("Application ID is required");

        RuleFor(x => x.EnvironmentName)
            .NotEmpty().WithMessage("Environment name is required")
            .Matches("^[a-z0-9-]+$").WithMessage("Environment name must be lowercase");

        RuleFor(x => x.RiskTier)
            .NotEmpty().WithMessage("Risk tier is required")
            .Must(BeValidRiskTier).WithMessage("Must be: critical, high, medium, low");

        RuleFor(x => x.SecurityTools)
            .NotEmpty().WithMessage("At least one security tool must be specified");

        RuleFor(x => x.PolicyReferences)
            .NotEmpty().WithMessage("At least one policy must be specified");
    }

    private bool BeValidRiskTier(string riskTier)
    {
        var validTiers = new[] { "critical", "high", "medium", "low" };
        return validTiers.Contains(riskTier?.ToLowerInvariant());
    }
}
```

**Validation Pipeline:**
```
Command/Query
   │
   ├─> FluentValidation Validator
   │     │
   │     ├─> Field Validation
   │     ├─> Business Rule Validation
   │     └─> Cross-Field Validation
   │
   ├─> If Invalid: Return ValidationException
   │
   └─> If Valid: Pass to Handler
```

**Validator Guidelines:**
- ✅ One validator per command/query
- ✅ Validate format, length, required fields
- ✅ Validate business rule preconditions
- ✅ Use custom validation methods for complex rules
- ❌ Don't duplicate domain validation
- ❌ Don't make database calls in validators

### 7. Application Interfaces (`/Interfaces`)

Interfaces define contracts for external services (implemented in Infrastructure layer).

**IOpaClient:**
```csharp
public interface IOpaClient
{
    Task<OpaDecisionDto> EvaluatePolicyAsync(
        OpaInputDto input,
        string policyPackage,
        CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
```

**INotificationService:**
```csharp
public interface INotificationService
{
    Task SendComplianceNotificationAsync(
        string applicationName,
        string environment,
        bool passed,
        IReadOnlyList<string> violations,
        IReadOnlyList<string> recipients,
        CancellationToken cancellationToken = default);

    Task SendCriticalVulnerabilityAlertAsync(
        string applicationName,
        string environment,
        int criticalCount,
        int highCount,
        IReadOnlyList<string> recipients,
        CancellationToken cancellationToken = default);
}
```

**Interface Design Guidelines:**
- ✅ Define in Application, implement in Infrastructure
- ✅ Use DTOs for parameters (not domain objects)
- ✅ Include CancellationToken support
- ✅ Return Task<T> for async operations
- ❌ Don't expose infrastructure details

### 8. Dependency Injection (`DependencyInjection.cs`)

Register all Application services with the DI container.

**Usage in API/Web project:**
```csharp
services.AddApplicationServices(); // Register MediatR and FluentValidation
```

**Registered Services:**
- All MediatR command/query handlers
- All FluentValidation validators

---

## Development Guidelines

### Adding a New Command

**1. Create Command Record:**
```csharp
// Commands/DeactivateApplicationCommand.cs
public record DeactivateApplicationCommand : IRequest<Result<ApplicationDto>>
{
    public required Guid ApplicationId { get; init; }
    public required string Reason { get; init; }
}
```

**2. Create Validator:**
```csharp
// Validators/DeactivateApplicationCommandValidator.cs
public class DeactivateApplicationCommandValidator
    : AbstractValidator<DeactivateApplicationCommand>
{
    public DeactivateApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
```

**3. Create Handler:**
```csharp
// Handlers/Commands/DeactivateApplicationCommandHandler.cs
public class DeactivateApplicationCommandHandler
    : IRequestHandler<DeactivateApplicationCommand, Result<ApplicationDto>>
{
    private readonly IApplicationRepository _repository;

    public async Task<Result<ApplicationDto>> Handle(
        DeactivateApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application == null)
            return Result.Failure<ApplicationDto>("Application not found");

        var result = application.Deactivate(request.Reason);
        if (result.IsFailure)
            return Result.Failure<ApplicationDto>(result.Error);

        await _repository.UpdateAsync(application, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(application));
    }
}
```

**4. Use in API Controller:**
```csharp
[HttpPost("api/applications/{id}/deactivate")]
public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateRequest request)
{
    var command = new DeactivateApplicationCommand
    {
        ApplicationId = id,
        Reason = request.Reason
    };

    var result = await _mediator.Send(command);

    return result.IsSuccess
        ? Ok(result.Value)
        : BadRequest(result.Error);
}
```

### Adding a New Query

**1. Create Query Record:**
```csharp
// Queries/GetApplicationsByOwnerQuery.cs
public record GetApplicationsByOwnerQuery : IRequest<Result<IReadOnlyList<ApplicationDto>>>
{
    public required string Owner { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
```

**2. Create Handler:**
```csharp
// Handlers/Queries/GetApplicationsByOwnerQueryHandler.cs
public class GetApplicationsByOwnerQueryHandler
    : IRequestHandler<GetApplicationsByOwnerQuery, Result<IReadOnlyList<ApplicationDto>>>
{
    private readonly IApplicationRepository _repository;

    public async Task<Result<IReadOnlyList<ApplicationDto>>> Handle(
        GetApplicationsByOwnerQuery request,
        CancellationToken cancellationToken)
    {
        var applications = await _repository.GetByOwnerAsync(request.Owner, cancellationToken);

        var skip = (request.PageNumber - 1) * request.PageSize;
        var paged = applications.Skip(skip).Take(request.PageSize);

        var dtos = paged.Select(MapToDto).ToList();

        return Result.Success<IReadOnlyList<ApplicationDto>>(dtos);
    }
}
```

### Adding a New DTO

**1. Create DTO with required/nullable pattern:**
```csharp
// DTOs/ApplicationStatisticsDto.cs
public class ApplicationStatisticsDto
{
    public required int TotalApplications { get; init; }
    public required int CriticalRiskCount { get; init; }
    public required int HighRiskCount { get; init; }
    public required Dictionary<string, int> ByRiskTier { get; init; }
    public DateTime? LastEvaluatedAt { get; init; }
}
```

### Adding a New External Service Interface

**1. Define interface in Application layer:**
```csharp
// Interfaces/IVulnerabilityEnrichmentService.cs
public interface IVulnerabilityEnrichmentService
{
    Task<VulnerabilityDetailsDto> EnrichAsync(
        string cveId,
        CancellationToken cancellationToken = default);
}
```

**2. Use in handler:**
```csharp
public class EvaluateComplianceCommandHandler : IRequestHandler<...>
{
    private readonly IVulnerabilityEnrichmentService _enrichmentService;

    public async Task<Result<ComplianceEvaluationDto>> Handle(...)
    {
        foreach (var vuln in vulnerabilities)
        {
            var enriched = await _enrichmentService.EnrichAsync(vuln.CveId);
            // Use enriched data...
        }
    }
}
```

**3. Implement in Infrastructure layer:**
```csharp
// Infrastructure/ExternalServices/NvdVulnerabilityEnrichmentService.cs
public class NvdVulnerabilityEnrichmentService : IVulnerabilityEnrichmentService
{
    // Implementation using NVD API
}
```

---

## Testing Strategy

### Unit Testing Handlers

**Test Structure:**
```csharp
public class EvaluateComplianceCommandHandlerTests
{
    private readonly Mock<IApplicationRepository> _applicationRepoMock;
    private readonly Mock<IOpaClient> _opaClientMock;
    private readonly EvaluateComplianceCommandHandler _handler;

    public EvaluateComplianceCommandHandlerTests()
    {
        _applicationRepoMock = new Mock<IApplicationRepository>();
        _opaClientMock = new Mock<IOpaClient>();
        _handler = new EvaluateComplianceCommandHandler(
            _applicationRepoMock.Object,
            _opaClientMock.Object,
            // ... other mocks
        );
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        // Arrange
        var application = CreateTestApplication();
        _applicationRepoMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(application);

        var opaDecision = new OpaDecisionDto { Allow = true, Violations = [] };
        _opaClientMock
            .Setup(x => x.EvaluatePolicyAsync(It.IsAny<OpaInputDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(opaDecision);

        var command = new EvaluateComplianceCommand
        {
            ApplicationId = application.Id,
            Environment = "production",
            ScanResults = [],
            InitiatedBy = "user@example.com"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        _applicationRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ApplicationNotFound_ReturnsFailure()
    {
        // Arrange
        _applicationRepoMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application)null);

        var command = new EvaluateComplianceCommand { /* ... */ };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }
}
```

### Unit Testing Validators

**Test Structure:**
```csharp
public class RegisterApplicationCommandValidatorTests
{
    private readonly RegisterApplicationCommandValidator _validator;

    public RegisterApplicationCommandValidatorTests()
    {
        _validator = new RegisterApplicationCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        // Arrange
        var command = new RegisterApplicationCommand
        {
            Name = "my-app",
            Owner = "team@example.com"
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "owner")] // Empty name
    [InlineData("app", "")] // Empty owner
    public void Validate_InvalidCommand_FailsValidation(string name, string owner)
    {
        // Arrange
        var command = new RegisterApplicationCommand
        {
            Name = name,
            Owner = owner
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
    }
}
```

### Integration Testing with MediatR

**Test Structure:**
```csharp
public class CommandPipelineIntegrationTests : IClassFixture<ApplicationFixture>
{
    private readonly IServiceProvider _serviceProvider;

    [Fact]
    public async Task SendCommand_ValidCommand_ExecutesSuccessfully()
    {
        // Arrange
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var command = new RegisterApplicationCommand
        {
            Name = "test-app",
            Owner = "test@example.com"
        };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
```

---

## Scalability Patterns

### 1. Pagination

**Always paginate query results:**
```csharp
public record GetAuditLogsQuery : IRequest<Result<IReadOnlyList<AuditLogDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50; // Default to 50, max 100
}
```

**Handler implementation:**
```csharp
var skip = (request.PageNumber - 1) * request.PageSize;
var pageSize = Math.Min(request.PageSize, 100); // Cap at 100
var results = query.Skip(skip).Take(pageSize).ToList();
```

### 2. Async/Await Everywhere

**All I/O operations must be async:**
```csharp
// ✅ Correct
var application = await _repository.GetByIdAsync(id, cancellationToken);
await _opaClient.EvaluatePolicyAsync(input, policy, cancellationToken);

// ❌ Incorrect
var application = _repository.GetByIdAsync(id, cancellationToken).Result; // BLOCKS THREAD
```

### 3. Fire-and-Forget for Non-Critical Operations

**Notifications should not block critical path:**
```csharp
// After saving evaluation and audit log...

// Fire-and-forget notification (don't await)
_ = Task.Run(async () =>
{
    try
    {
        await _notificationService.SendComplianceNotificationAsync(/* ... */);
    }
    catch
    {
        // Log but don't fail the evaluation
    }
}, cancellationToken);

return Result.Success(evaluationDto);
```

### 4. Caching Read-Heavy Data

**For frequently accessed, rarely changing data:**
```csharp
public class GetApplicationByIdQueryHandler : IRequestHandler<...>
{
    private readonly IMemoryCache _cache;
    private readonly IApplicationRepository _repository;

    public async Task<Result<ApplicationDto>> Handle(...)
    {
        var cacheKey = $"app_{request.ApplicationId}";

        if (_cache.TryGetValue(cacheKey, out ApplicationDto cached))
            return Result.Success(cached);

        var application = await _repository.GetByIdAsync(request.ApplicationId);
        var dto = MapToDto(application);

        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));

        return Result.Success(dto);
    }
}
```

### 5. Background Processing with Hangfire/Quartz

**For long-running operations:**
```csharp
// Instead of blocking, enqueue background job
public class RecalculateRiskScoresCommand : IRequest<Result<JobId>>
{
    public required Guid ApplicationId { get; init; }
}

public class RecalculateRiskScoresCommandHandler : IRequestHandler<...>
{
    private readonly IBackgroundJobClient _backgroundJobs;

    public async Task<Result<JobId>> Handle(...)
    {
        var jobId = _backgroundJobs.Enqueue<RiskCalculationJob>(
            job => job.CalculateAsync(request.ApplicationId));

        return Result.Success(new JobId(jobId));
    }
}
```

---

## Common Patterns

### Railway-Oriented Programming with Result<T>

**Chain operations that can fail:**
```csharp
public async Task<Result<ComplianceEvaluationDto>> Handle(...)
{
    // Each operation returns Result
    var applicationResult = await GetApplicationAsync(request.ApplicationId);
    if (applicationResult.IsFailure)
        return Result.Failure<ComplianceEvaluationDto>(applicationResult.Error);

    var scanResultsResult = MapScanResults(request.ScanResults);
    if (scanResultsResult.IsFailure)
        return Result.Failure<ComplianceEvaluationDto>(scanResultsResult.Error);

    var evaluationResult = ComplianceEvaluation.Create(/* ... */);
    if (evaluationResult.IsFailure)
        return Result.Failure<ComplianceEvaluationDto>(evaluationResult.Error);

    // All succeeded, return success
    return Result.Success(MapToDto(evaluationResult.Value));
}
```

### Transaction Boundaries

**SaveChangesAsync defines transaction boundary:**
```csharp
// All changes within one transaction
await _evaluationRepository.AddAsync(evaluation);
await _auditLogRepository.AddAsync(auditLog);
await _evaluationRepository.SaveChangesAsync(); // Commits both or rolls back both
```

### Mapping Domain to DTOs

**Use private methods for mapping:**
```csharp
private static ApplicationDto MapToDto(Application application)
{
    return new ApplicationDto
    {
        Id = application.Id,
        Name = application.Name,
        Owner = application.Owner,
        Environments = application.Environments.Select(e => new EnvironmentConfigDto
        {
            Id = e.Id,
            Name = e.Name,
            RiskTier = e.RiskTier.Value, // Extract from environment, not application
            SecurityTools = e.SecurityTools.Select(t => t.Value).ToList(),
            PolicyReferences = e.PolicyReferences.Select(p => p.PackageName).ToList(),
            IsActive = e.IsActive
        }).ToList(),
        CreatedAt = application.CreatedAt,
        UpdatedAt = application.UpdatedAt
    };
}
```

---

## Summary

The Application layer is the **orchestration layer** that:
- ✅ Coordinates domain operations (commands/queries)
- ✅ Defines transaction boundaries
- ✅ Maps domain models to DTOs
- ✅ Validates input
- ✅ Calls external services (OPA, notifications)
- ❌ Does NOT contain business logic
- ❌ Does NOT know about databases or HTTP

**Key Technologies:**
- **MediatR** for CQRS pattern
- **FluentValidation** for input validation
- **Result<T>** for error handling without exceptions

**Next Steps:**
1. Implement Infrastructure layer (repositories, OPA client, notifications)
2. Build API layer (controllers, middleware)
3. Write integration tests

For Domain layer details, see: [`../ComplianceService.Domain/README.md`](../ComplianceService.Domain/README.md)
