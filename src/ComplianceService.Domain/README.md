# ComplianceService.Domain

**The heart of the ComplianceService application - pure business logic with zero dependencies.**

## Purpose

The Domain layer contains all business logic, rules, and invariants for compliance evaluation. It represents the **ubiquitous language** of the compliance domain and is completely independent of infrastructure, frameworks, and external concerns.

Following Domain-Driven Design (DDD) principles, this layer defines:
- **What** the system does (business rules)
- **Why** operations succeed or fail (domain validation)
- **How** domain concepts relate (aggregates, entities, value objects)

## Architecture Philosophy

### Clean Architecture Principles

```
┌─────────────────────────────────────────────┐
│  Domain Layer (This Layer)                  │
│  ┌─────────────────────────────────────┐   │
│  │  No External Dependencies            │   │
│  │  Pure C# 12                          │   │
│  │  Business Logic Only                 │   │
│  └─────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
         ▲                    ▲
         │                    │
         │                    │
    Application          Infrastructure
    Layer (uses)         Layer (implements)
```

**Key Principles:**
- ✅ **Dependency Inversion**: Domain defines interfaces, Infrastructure implements them
- ✅ **Independence**: Can be tested without databases, APIs, or frameworks
- ✅ **Stability**: Changes to UI/API/Database don't affect domain logic
- ✅ **Expressiveness**: Code reads like business requirements

## Project Structure

```
ComplianceService.Domain/
├── Shared/                          # Domain primitives (base classes)
│   ├── Entity.cs                    # Identity-based entities
│   ├── ValueObject.cs               # Value-based objects
│   ├── AggregateRoot.cs            # Aggregate roots with events
│   ├── IDomainEvent.cs             # Event marker interface
│   └── Result.cs                    # Railway-oriented programming
│
├── ApplicationProfile/              # Application registration context
│   ├── Application.cs               # Aggregate root
│   ├── Entities/
│   │   └── EnvironmentConfig.cs    # Environment configuration
│   ├── ValueObjects/
│   │   ├── RiskTier.cs             # critical, high, medium, low
│   │   ├── PolicyReference.cs      # OPA policy references
│   │   └── SecurityToolType.cs     # snyk, prismacloud
│   ├── Events/
│   │   ├── ApplicationRegisteredEvent.cs
│   │   └── EnvironmentConfigUpdatedEvent.cs
│   └── Interfaces/
│       └── IApplicationRepository.cs
│
├── Evaluation/                      # Compliance evaluation context
│   ├── ComplianceEvaluation.cs     # Aggregate root
│   ├── ValueObjects/
│   │   ├── ScanResult.cs           # Tool scan output
│   │   ├── Vulnerability.cs        # CVE details
│   │   ├── VulnerabilitySeverity.cs
│   │   └── PolicyDecision.cs       # OPA decision
│   ├── Events/
│   │   └── ComplianceEvaluationCompletedEvent.cs
│   └── Interfaces/
│       └── IComplianceEvaluationRepository.cs
│
└── Audit/                           # Audit trail context
    ├── AuditLog.cs                 # Aggregate root (immutable)
    ├── ValueObjects/
    │   └── DecisionEvidence.cs     # Complete evidence storage
    └── Interfaces/
        └── IAuditLogRepository.cs
```

## Bounded Contexts

The domain is organized into three **bounded contexts** - each with its own ubiquitous language and consistency boundaries:

### 1. Application Profile Context
**Responsibility:** Managing application registrations and environment configurations

**Core Concept:** Applications have different security requirements based on risk tier and environment.

**Key Rules:**
- Application names must be unique
- Each environment must have at least one security tool configured
- Each environment must reference at least one OPA policy
- Risk tier determines policy stringency (critical apps = strictest)

### 2. Evaluation Context
**Responsibility:** Evaluating security scan results against compliance policies

**Core Concept:** Scan results from multiple tools are aggregated and evaluated by OPA policies.

**Key Rules:**
- At least one scan result required for evaluation
- Vulnerabilities counted across all security tools
- Policy decision determines deployment allowance
- Evaluation is immutable once created

### 3. Audit Context
**Responsibility:** Maintaining complete audit trail for compliance reporting

**Core Concept:** Every compliance decision is permanently recorded with complete evidence.

**Key Rules:**
- Audit logs are append-only (never updated or deleted)
- Complete evidence stored as JSON for regulatory compliance
- Aggregated vulnerability counts for quick querying
- Timestamps and duration tracked for SLA monitoring

## Domain-Driven Design Patterns

### Aggregates

**What:** Cluster of domain objects treated as a single unit for data changes.

**Why:** Enforces consistency boundaries - ensures invariants are always satisfied.

**Aggregates in this domain:**
- `Application` - Consistency boundary for app profiles and environments
- `ComplianceEvaluation` - Consistency boundary for scan results and decisions
- `AuditLog` - Immutable record of a compliance decision

**Rules:**
- Only aggregate roots are directly accessible via repositories
- External objects reference aggregates by ID (Guid), not object reference
- Aggregate roots enforce all invariants within their boundary

### Entities

**What:** Objects with identity that persists over time (compared by ID).

**Example:**
```csharp
public class EnvironmentConfig : Entity<Guid>
{
    public string EnvironmentName { get; private set; }
    // Two environments with same name are the same environment
}
```

**Characteristics:**
- Has unique identifier (Guid)
- Equality based on ID, not property values
- Can change state over time
- Lives within an aggregate boundary

### Value Objects

**What:** Objects without identity, compared by value (immutable).

**Example:**
```csharp
public sealed class RiskTier : ValueObject
{
    public string Value { get; }
    public static readonly RiskTier Critical = new("critical");

    // Two "critical" risk tiers are identical, no matter where created
}
```

**Characteristics:**
- Immutable - state cannot change after creation
- Equality by value - all properties must match
- Can be freely shared and copied
- Should be small and focused

**When to use:**
- Descriptive concepts (RiskTier, Severity)
- Measurements (CvssScore in Vulnerability)
- Complex values (ScanResult with multiple properties)

### Domain Events

**What:** Something interesting that happened in the domain.

**Example:**
```csharp
public sealed class ApplicationRegisteredEvent : IDomainEvent
{
    public Guid ApplicationId { get; }
    public DateTime OccurredOn { get; }
}
```

**Purpose:**
- Decouple aggregates - Application doesn't need to know about Audit
- Enable eventual consistency across bounded contexts
- Support event sourcing and audit trails
- Trigger side effects (send notifications, update read models)

**Usage Pattern:**
```csharp
// In aggregate root
var application = new Application(...);
application.AddDomainEvent(new ApplicationRegisteredEvent(...));

// Events collected and published after saving
// Infrastructure layer publishes via MediatR or message bus
```

### Repository Pattern

**What:** Abstraction for data access - hides persistence details from domain.

**Example:**
```csharp
public interface IApplicationRepository
{
    Task<Result<Application>> GetByIdAsync(Guid id);
    Task<Result> AddAsync(Application application);
}
```

**Benefits:**
- Domain doesn't depend on EF Core, Dapper, or any ORM
- Can test domain logic with in-memory repositories
- Can swap database technologies without changing domain
- Enforces aggregate boundaries (only aggregates have repositories)

### Result Type (Railway-Oriented Programming)

**What:** Explicit success/failure without throwing exceptions.

**Example:**
```csharp
public static Result<Application> Create(string name, RiskTier riskTier, string owner)
{
    if (string.IsNullOrWhiteSpace(name))
        return Result.Failure<Application>("Application name cannot be empty");

    return Result.Success(new Application(name, riskTier, owner));
}

// Usage
var result = Application.Create("payment-api", RiskTier.Critical, "team@company.com");
if (result.IsFailure)
{
    // Handle error: result.Error
}
else
{
    // Use value: result.Value
}
```

**Benefits:**
- Makes failures explicit and type-safe
- Forces caller to handle errors
- No hidden control flow (exceptions are hidden goto statements)
- Easier to test error scenarios

## Development Guide

### Adding a New Aggregate

**Scenario:** You need to add "PolicyTemplate" to manage reusable policy configurations.

**Steps:**

1. **Create bounded context directory:**
```bash
mkdir -p src/ComplianceService.Domain/PolicyTemplate/{Entities,ValueObjects,Events,Interfaces}
```

2. **Define the aggregate root:**
```csharp
public class PolicyTemplate : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    // ... properties

    private PolicyTemplate() : base() { }

    public static Result<PolicyTemplate> Create(string name, string description)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<PolicyTemplate>("Name required");

        var template = new PolicyTemplate { ... };
        template.AddDomainEvent(new PolicyTemplateCreatedEvent(...));
        return Result.Success(template);
    }
}
```

3. **Create value objects for domain concepts:**
```csharp
public sealed class PolicySeverityThreshold : ValueObject
{
    public int MaxCritical { get; }
    public int MaxHigh { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MaxCritical;
        yield return MaxHigh;
    }
}
```

4. **Define domain events:**
```csharp
public sealed class PolicyTemplateCreatedEvent : IDomainEvent
{
    public Guid TemplateId { get; }
    public DateTime OccurredOn { get; }
}
```

5. **Define repository interface:**
```csharp
public interface IPolicyTemplateRepository
{
    Task<Result<PolicyTemplate>> GetByIdAsync(Guid id);
    Task<Result> AddAsync(PolicyTemplate template);
}
```

6. **Create README.md** in PolicyTemplate/ explaining the context

### Adding a New Value Object

**Example:** Adding "EmailAddress" value object

```csharp
public sealed class EmailAddress : ValueObject
{
    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    public static Result<EmailAddress> Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<EmailAddress>("Email cannot be empty");

        if (!IsValidEmail(email))
            return Result.Failure<EmailAddress>($"Invalid email: {email}");

        return Result.Success(new EmailAddress(email.Trim().ToLowerInvariant()));
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

**When to create a value object:**
- ✅ Concept has validation rules (email format, CVSS 0-10 range)
- ✅ Represents a domain concept (not just a primitive)
- ✅ Equality by value makes sense (two "critical" tiers are identical)
- ✅ Immutability is desired
- ❌ Don't create for simple IDs (Guid is fine as-is)

### Adding Business Rules

**Location:** Business rules belong in the aggregate root or entity methods.

**Example:** Adding rule "Critical applications in production cannot have any active violations"

```csharp
public class Application : AggregateRoot<Guid>
{
    public Result AllowProductionDeployment(List<Violation> violations)
    {
        if (!RiskTier.IsCritical)
            return Result.Success(); // Rule doesn't apply

        if (violations.Any(v => v.IsActive))
            return Result.Failure(
                "Critical applications cannot deploy to production with active violations");

        return Result.Success();
    }
}
```

**Guidelines:**
- Use descriptive method names that express intent
- Return `Result` for operations that can fail
- Throw exceptions only for programmer errors (never for business rule violations)
- Keep rules in domain, not in application services

### Extending Existing Aggregates

**Scenario:** Adding "Description" field to Application

**Safe process:**

1. **Add property with private setter:**
```csharp
public class Application : AggregateRoot<Guid>
{
    public string Description { get; private set; } = string.Empty;
}
```

2. **Update factory method to accept new parameter:**
```csharp
public static Result<Application> Create(
    string name,
    RiskTier riskTier,
    string owner,
    string? description = null) // Optional for backwards compatibility
{
    // ... validation

    return Result.Success(new Application(
        name,
        riskTier,
        owner,
        description ?? string.Empty)); // Default if not provided
}
```

3. **Add update method if field can change after creation:**
```csharp
public Result UpdateDescription(string newDescription)
{
    if (string.IsNullOrWhiteSpace(newDescription))
        return Result.Failure("Description cannot be empty");

    if (newDescription.Length > 500)
        return Result.Failure("Description too long (max 500 characters)");

    Description = newDescription.Trim();
    MarkAsUpdated();

    return Result.Success();
}
```

4. **Update tests** to cover new validation rules

## Testing Guidelines

### Unit Testing Domain Logic

Domain layer should have **highest test coverage** (aim for 90%+) since it contains all business rules.

**Example test structure:**
```csharp
public class ApplicationTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var name = "payment-api";
        var riskTier = RiskTier.Critical;
        var owner = "team@company.com";

        // Act
        var result = Application.Create(name, riskTier, owner);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(name);
        result.Value.RiskTier.Should().Be(riskTier);
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        // Act
        var result = Application.Create("", RiskTier.Critical, "team@company.com");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name cannot be empty");
    }

    [Fact]
    public void AddEnvironment_WhenEnvironmentAlreadyExists_ShouldFail()
    {
        // Arrange
        var application = Application.Create(...).Value;
        var env = EnvironmentConfig.Create(..., "production", ...).Value;
        application.AddEnvironment(env);

        // Act - try to add same environment again
        var result = application.AddEnvironment(env);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }
}
```

**Testing tools:**
- xUnit - Test framework
- FluentAssertions - Readable assertions
- No mocking needed! Domain logic is pure and self-contained

**What to test:**
- ✅ Factory methods (Create) with valid and invalid inputs
- ✅ Business rule enforcement
- ✅ State transitions (activate/deactivate)
- ✅ Domain event raising
- ✅ Value object equality
- ✅ Edge cases (empty strings, null, max lengths)
- ❌ Don't test private methods - test through public API
- ❌ Don't test properties - they're data containers

### Test Organization

```
tests/
└── ComplianceService.Domain.Tests/
    ├── ApplicationProfile/
    │   ├── ApplicationTests.cs
    │   ├── EnvironmentConfigTests.cs
    │   └── ValueObjects/
    │       ├── RiskTierTests.cs
    │       └── PolicyReferenceTests.cs
    ├── Evaluation/
    │   ├── ComplianceEvaluationTests.cs
    │   └── ValueObjects/
    │       ├── VulnerabilityTests.cs
    │       └── ScanResultTests.cs
    └── Audit/
        └── AuditLogTests.cs
```

## Scalability & Growth Patterns

### Horizontal Scalability

**Stateless Design:**
- Domain objects are created per request, not cached
- No static mutable state
- Aggregates are loaded, modified, and saved independently
- Enables horizontal scaling across multiple instances

**Event-Driven Architecture:**
- Domain events enable asynchronous processing
- Heavy operations (sending notifications, updating read models) moved to background
- Main request only persists aggregate changes

### Performance Optimization

**Aggregate Size:**
- Keep aggregates small - don't load entire object graph
- Load only necessary child entities
- Use projections for read-heavy operations (query side of CQRS)

**Example - Don't do this:**
```csharp
// Loading 1000 environments just to check if one exists
var application = await repository.GetByIdAsync(id);
var hasProduction = application.Environments.Any(e => e.Name == "production");
```

**Do this instead:**
```csharp
// Query side - direct database query
var hasProduction = await repository.HasEnvironmentAsync(id, "production");
```

### Extending with New Features

**Adding new bounded context: "Template" for policy templates**

1. Create new directory under Domain/
2. Define aggregate root, entities, value objects
3. Define repository interface
4. Domain events for integration with other contexts
5. No changes needed to existing contexts (loose coupling)

**Adding new security tool: "Trivy"**

1. Update `SecurityToolType` value object:
```csharp
public static readonly SecurityToolType Trivy = new("trivy");

public static Result<SecurityToolType> Create(string name)
{
    return normalized switch
    {
        "snyk" => Result.Success(Snyk),
        "prismacloud" => Result.Success(PrismaCloud),
        "trivy" => Result.Success(Trivy), // Add new tool
        _ => Result.Failure<SecurityToolType>($"Unsupported: {name}")
    };
}
```

2. Update tests
3. No changes needed to Application or Evaluation aggregates!

### Versioning Strategies

**Adding optional fields:**
- Add with default values
- Make constructor parameter optional
- Existing code continues to work

**Removing fields:**
- Mark as `[Obsolete]` first
- Give teams time to migrate
- Remove in next major version

**Breaking changes:**
- Introduce new aggregate version (ApplicationV2)
- Run both versions in parallel
- Migrate data gradually
- Deprecate old version

## Common Patterns & Best Practices

### Validation in Factory Methods

**Always validate in static Create() methods:**
```csharp
public static Result<Application> Create(string name, ...)
{
    if (string.IsNullOrWhiteSpace(name))
        return Result.Failure<Application>("Name required");

    if (name.Length > 100)
        return Result.Failure<Application>("Name too long (max 100)");

    return Result.Success(new Application(name, ...));
}
```

**Why:**
- Impossible to create invalid aggregate
- Validation logic centralized
- Caller forced to handle failures

### Protected Constructors

**Always provide parameterless constructor for EF Core:**
```csharp
private Application() : base()
{
    // EF Core uses this via reflection
    Name = string.Empty;
    RiskTier = ValueObjects.RiskTier.Low;
}

private Application(string name, RiskTier riskTier, string owner) : base(Guid.NewGuid())
{
    // Domain uses this via Create() method
    Name = name;
    RiskTier = riskTier;
    Owner = owner;
}
```

### Private Setters

**Encapsulate all state changes:**
```csharp
public class Application : AggregateRoot<Guid>
{
    public string Name { get; private set; } // Can't be set from outside

    public void Rename(string newName)
    {
        // Validation
        Name = newName;
        MarkAsUpdated();
        AddDomainEvent(new ApplicationRenamedEvent(...));
    }
}
```

### Avoid Anemic Domain Model

**❌ Bad - No behavior, just data:**
```csharp
public class Application
{
    public string Name { get; set; }
    public bool IsActive { get; set; }
}

// Business logic in service
public class ApplicationService
{
    public void Deactivate(Application app)
    {
        app.IsActive = false; // Rule: should check if has active deployments
    }
}
```

**✅ Good - Rich behavior:**
```csharp
public class Application : AggregateRoot<Guid>
{
    public bool IsActive { get; private set; }

    public Result Deactivate()
    {
        if (HasActiveDeployments())
            return Result.Failure("Cannot deactivate with active deployments");

        IsActive = false;
        MarkAsUpdated();
        return Result.Success();
    }
}
```

## Troubleshooting

### "How do I handle cross-aggregate operations?"

**Use domain events:**
```csharp
// When Application is registered
application.AddDomainEvent(new ApplicationRegisteredEvent(application.Id, ...));

// Infrastructure publishes event
// AuditLogHandler creates initial audit record
```

**Or use application service to coordinate:**
```csharp
public class RegisterApplicationService
{
    public async Task<Result> Handle(RegisterApplicationCommand cmd)
    {
        // Create application
        var app = Application.Create(...);
        await _appRepo.AddAsync(app.Value);

        // Create initial audit record
        var audit = AuditLog.Create(...);
        await _auditRepo.AddAsync(audit.Value);

        return Result.Success();
    }
}
```

### "When should I create a new bounded context?"

**Create new context when:**
- Different teams own different parts of the domain
- Different release cadences needed
- Different data storage requirements
- Concepts don't naturally fit existing contexts

**Keep in same context when:**
- Strong consistency required between concepts
- Same team owns both areas
- Concepts are tightly coupled in business logic

### "How do I query across aggregates?"

**Don't load multiple aggregates!** Use read models (CQRS):
```csharp
// Query side - direct SQL or read model
public class ApplicationSummaryQuery
{
    public async Task<List<ApplicationSummary>> GetAllWithCounts()
    {
        // Query database directly, bypassing domain
        // Return DTOs optimized for UI
    }
}
```

## Further Reading

- **Domain-Driven Design** by Eric Evans (Blue Book)
- **Implementing Domain-Driven Design** by Vaughn Vernon (Red Book)
- **Domain Modeling Made Functional** by Scott Wlaschin
- **Enterprise Patterns and MDA** by Jim Arlow
- Martin Fowler's articles on Domain-Driven Design

---

**Remember:** The domain layer is the heart of your application. Invest time in making it correct, expressive, and maintainable. All other layers are just plumbing to support the domain.
