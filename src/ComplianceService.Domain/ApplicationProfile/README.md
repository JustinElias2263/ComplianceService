# Application Profile Bounded Context

**Manages application registrations and environment-specific compliance configurations.**

## Purpose

This bounded context is responsible for:
- Registering applications in the compliance system
- Defining risk tiers for applications (critical, high, medium, low)
- Managing environment configurations (production, staging, dev)
- Mapping security tools to environments
- Assigning OPA policies per environment

**Key principle:** Application profiles contain **metadata only** - no business logic thresholds. All compliance rules live in OPA policies.

## Domain Model

```
Application (Aggregate Root)
├── Properties
│   ├── Id: Guid
│   ├── Name: string (unique, 3-100 chars)
│   ├── RiskTier: RiskTier (value object)
│   ├── Owner: string (email address)
│   └── IsActive: bool
│
└── Environments: List<EnvironmentConfig>
    ├── Id: Guid
    ├── EnvironmentName: string (production, staging, dev, etc.)
    ├── SecurityTools: List<SecurityToolType> (snyk, prismacloud)
    ├── Policies: List<PolicyReference> (OPA policy packages)
    └── Metadata: Dictionary<string, string> (tool-specific config)
```

## Ubiquitous Language

- **Application**: A software system being evaluated for compliance
- **Risk Tier**: Classification determining policy stringency (critical = strictest)
- **Environment**: Deployment target (production, staging, dev) with different security requirements
- **Security Tool**: Scanner that produces vulnerability reports (Snyk, Prisma Cloud)
- **Policy Reference**: Name of an OPA Rego policy package to evaluate against
- **Owner**: Team or person responsible for the application (email address)

## Components

### Aggregate Root: Application

**File:** `Application.cs`

**Responsibility:** Ensures consistency of application profile and its environments.

**Key Methods:**
```csharp
// Factory method with validation
public static Result<Application> Create(string name, RiskTier riskTier, string owner)

// Add environment configuration
public Result AddEnvironment(EnvironmentConfig environmentConfig)

// Get specific environment
public Result<EnvironmentConfig> GetEnvironment(string environmentName)

// Update risk tier
public void UpdateRiskTier(RiskTier newRiskTier)

// Activate/deactivate application
public void Deactivate()
public void Activate()
```

**Invariants enforced:**
- ✅ Application name must be unique (enforced by repository)
- ✅ Owner must be valid email address
- ✅ Environment names must be unique within application
- ✅ Cannot add environment with wrong ApplicationId

**Domain Events:**
- `ApplicationRegisteredEvent` - Raised when new application created
- `EnvironmentConfigUpdatedEvent` - Raised when environment modified

### Entity: EnvironmentConfig

**File:** `Entities/EnvironmentConfig.cs`

**Responsibility:** Configuration for one environment of an application.

**Key Methods:**
```csharp
// Factory method
public static Result<EnvironmentConfig> Create(
    Guid applicationId,
    string environmentName,
    List<SecurityToolType> securityTools,
    List<PolicyReference> policies,
    Dictionary<string, string>? metadata = null)

// Update configured tools
public void UpdateSecurityTools(List<SecurityToolType> tools)

// Update assigned policies
public void UpdatePolicies(List<PolicyReference> policies)

// Manage metadata
public void UpdateMetadata(string key, string value)
public void RemoveMetadata(string key)
```

**Invariants:**
- ✅ At least one security tool must be configured
- ✅ At least one policy must be assigned
- ✅ Environment name must be valid (production, staging, dev, test, qa, uat)

### Value Objects

#### RiskTier
**File:** `ValueObjects/RiskTier.cs`

**Purpose:** Application risk classification determining policy stringency.

**Values:**
- `critical` - Payment processing, PII, financial systems (zero tolerance)
- `high` - Customer-facing, authentication (minimal tolerance)
- `medium` - Internal business apps (moderate tolerance)
- `low` - Development tools, utilities (relaxed policies)

**Usage:**
```csharp
var tierResult = RiskTier.Create("critical");
if (tierResult.IsSuccess)
{
    var tier = tierResult.Value;
    if (tier.IsCritical)
    {
        // Apply strictest policies
    }
}
```

#### PolicyReference
**File:** `ValueObjects/PolicyReference.cs`

**Purpose:** Reference to an OPA policy package.

**Format:** `compliance/policy-name` or `compliance.policy_name`

**Example:** `compliance/critical-production`

**Validation:**
- Must contain separator (/ or .)
- Length 3-200 characters
- Trimmed of whitespace

#### SecurityToolType
**File:** `ValueObjects/SecurityToolType.cs`

**Purpose:** Supported security scanning tools.

**Values:**
- `snyk` - Dependency vulnerability scanning
- `prismacloud` - Container and cloud security

**Extending:**
To add new tool (e.g., "trivy"):
```csharp
public static readonly SecurityToolType Trivy = new("trivy");

public static Result<SecurityToolType> Create(string name)
{
    return normalized switch
    {
        "snyk" => Result.Success(Snyk),
        "prismacloud" => Result.Success(PrismaCloud),
        "trivy" => Result.Success(Trivy), // Add here
        _ => Result.Failure<SecurityToolType>($"Unsupported: {name}")
    };
}
```

### Repository Interface

**File:** `Interfaces/IApplicationRepository.cs`

**Purpose:** Persistence abstraction (implemented in Infrastructure layer).

**Key Methods:**
```csharp
Task<Result<Application>> GetByIdAsync(Guid id);
Task<Result<Application>> GetByNameAsync(string name);
Task<IReadOnlyList<Application>> GetByRiskTierAsync(string riskTier);
Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null);
Task<Result> AddAsync(Application application);
Task<Result> UpdateAsync(Application application);
```

## Use Cases

### 1. Register New Application

```csharp
// Create application
var tierResult = RiskTier.Create("critical");
var appResult = Application.Create(
    "payment-processing-api",
    tierResult.Value,
    "payments-team@company.com"
);

if (appResult.IsFailure)
    return Result.Failure(appResult.Error);

var application = appResult.Value;

// Add production environment
var prodTools = new List<SecurityToolType> {
    SecurityToolType.Snyk,
    SecurityToolType.PrismaCloud
};

var prodPolicies = new List<PolicyReference> {
    PolicyReference.Create("compliance/critical-production").Value,
    PolicyReference.Create("compliance/zero-critical-vulns").Value
};

var prodEnvResult = EnvironmentConfig.Create(
    application.Id,
    "production",
    prodTools,
    prodPolicies,
    new Dictionary<string, string> {
        ["snykProjectId"] = "abc-123",
        ["prismaProjectId"] = "xyz-789"
    }
);

application.AddEnvironment(prodEnvResult.Value);

// Save
await repository.AddAsync(application);
```

### 2. Update Risk Tier

```csharp
var application = await repository.GetByIdAsync(appId);
if (application.IsFailure)
    return application.Error;

var newTierResult = RiskTier.Create("high");
application.Value.UpdateRiskTier(newTierResult.Value);

await repository.UpdateAsync(application.Value);
```

### 3. Add Staging Environment

```csharp
var application = await repository.GetByIdAsync(appId);

var stagingTools = new List<SecurityToolType> { SecurityToolType.Snyk };
var stagingPolicies = new List<PolicyReference> {
    PolicyReference.Create("compliance/critical-staging").Value
};

var stagingEnvResult = EnvironmentConfig.Create(
    application.Value.Id,
    "staging",
    stagingTools,
    stagingPolicies
);

var addResult = application.Value.AddEnvironment(stagingEnvResult.Value);
if (addResult.IsFailure)
    return addResult.Error;

await repository.UpdateAsync(application.Value);
```

## Development Guidelines

### Adding New Risk Tier

If you need a new tier beyond critical/high/medium/low:

```csharp
// 1. Add to RiskTier value object
public static readonly RiskTier Regulatory = new("regulatory");

// 2. Update Create method
public static Result<RiskTier> Create(string value)
{
    return normalized switch
    {
        "critical" => Result.Success(Critical),
        "high" => Result.Success(High),
        "medium" => Result.Success(Medium),
        "low" => Result.Success(Low),
        "regulatory" => Result.Success(Regulatory), // Add here
        _ => Result.Failure<RiskTier>("Invalid risk tier")
    };
}

// 3. Update OPA policies to handle new tier
// 4. Update tests
// 5. Update API documentation
```

### Adding Environment-Specific Business Rule

**Example:** Production environments for critical apps must have both Snyk AND Prisma Cloud.

```csharp
public class EnvironmentConfig : Entity<Guid>
{
    public Result ValidateForProduction(RiskTier applicationRiskTier)
    {
        if (!IsProduction)
            return Result.Success();

        if (applicationRiskTier.IsCritical)
        {
            bool hasSnyk = SecurityTools.Any(t => t.Name == "snyk");
            bool hasPrisma = SecurityTools.Any(t => t.Name == "prismacloud");

            if (!hasSnyk || !hasPrisma)
                return Result.Failure(
                    "Critical applications in production must have both Snyk and Prisma Cloud");
        }

        return Result.Success();
    }
}

// Call during AddEnvironment
public Result AddEnvironment(EnvironmentConfig environmentConfig)
{
    var validationResult = environmentConfig.ValidateForProduction(RiskTier);
    if (validationResult.IsFailure)
        return validationResult;

    // ... rest of logic
}
```

### Adding Custom Metadata

Metadata is flexible key-value storage for tool-specific configuration:

```csharp
var env = EnvironmentConfig.Create(...).Value;

// Add Snyk project ID
env.UpdateMetadata("snykProjectId", "abc-123-def-456");

// Add Prisma project ID
env.UpdateMetadata("prismaProjectId", "xyz-789");

// Add custom notification channel
env.UpdateMetadata("slackChannel", "#security-alerts");

// Remove metadata
env.RemoveMetadata("snykProjectId");
```

**Best practices:**
- Use consistent key naming (camelCase)
- Store only configuration, not secrets
- Don't query metadata in domain logic (use for storage only)
- Document expected keys in application service layer

## Testing

### Test Application Invariants

```csharp
[Fact]
public void Create_WithEmptyName_ShouldFail()
{
    var result = Application.Create("", RiskTier.Critical, "test@test.com");

    result.IsFailure.Should().BeTrue();
    result.Error.Should().Contain("name");
}

[Fact]
public void AddEnvironment_WhenAlreadyExists_ShouldFail()
{
    var app = Application.Create("test", RiskTier.Critical, "test@test.com").Value;
    var env1 = EnvironmentConfig.Create(app.Id, "production", ...).Value;
    var env2 = EnvironmentConfig.Create(app.Id, "production", ...).Value;

    app.AddEnvironment(env1);
    var result = app.AddEnvironment(env2);

    result.IsFailure.Should().BeTrue();
    result.Error.Should().Contain("already exists");
}
```

### Test Value Object Validation

```csharp
[Theory]
[InlineData("critical")]
[InlineData("CRITICAL")]
[InlineData("  critical  ")]
public void RiskTier_Create_WithValidValue_ShouldSucceed(string value)
{
    var result = RiskTier.Create(value);

    result.IsSuccess.Should().BeTrue();
    result.Value.Value.Should().Be("critical"); // Normalized
}

[Fact]
public void PolicyReference_WithoutSeparator_ShouldFail()
{
    var result = PolicyReference.Create("invalidpolicy");

    result.IsFailure.Should().BeTrue();
    result.Error.Should().Contain("separator");
}
```

## Scalability Considerations

### Handling Large Numbers of Applications

**Scenario:** System grows to 10,000+ applications

**Solutions:**
1. **Pagination in queries:**
```csharp
Task<PagedResult<Application>> GetAllAsync(int pageSize, int pageNumber);
```

2. **Filtering by risk tier:**
```csharp
// Most queries focus on critical/high applications
var criticalApps = await repository.GetByRiskTierAsync("critical");
```

3. **Read models for UI:**
```csharp
// Don't load full aggregate for lists
public record ApplicationSummary(Guid Id, string Name, string RiskTier, int EnvironmentCount);
```

### Many Environments Per Application

**Scenario:** Application has 20+ environments (dev1, dev2, ..., dev20)

**Current design handles this well:**
- Environments loaded with aggregate (lazy loading possible with EF Core)
- GetEnvironment() retrieves specific environment without loading all

**If performance becomes issue:**
- Move to separate EnvironmentConfig aggregate
- Reference by ID from Application
- Query environments independently

### Global Uniqueness Constraints

**Application names must be globally unique.**

**Implementation:**
- Unique index in database
- Repository method `IsNameUniqueAsync` for validation before save
- Handle concurrent creation with unique constraint violations

```csharp
try
{
    await repository.AddAsync(application);
}
catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
{
    return Result.Failure("Application name already exists");
}
```

## Integration with Other Contexts

### Evaluation Context
- Evaluation reads Application to get RiskTier and Environment config
- Uses EnvironmentConfig.Policies to know which OPA policies to evaluate
- No direct dependency - communicates via IDs

### Audit Context
- Audit logs reference ApplicationId and store ApplicationName
- When Application name changes, audit logs keep historical name (immutable)
- Domain event could trigger update to read model if needed

## Common Pitfalls

### ❌ Storing Thresholds in Application Profile

```csharp
// WRONG - thresholds belong in OPA policies
public class EnvironmentConfig
{
    public int MaxCriticalVulnerabilities { get; set; } // NO!
    public int MaxHighVulnerabilities { get; set; }     // NO!
}
```

**Why wrong:** Compliance team can't update thresholds without deploying code changes.

**Correct approach:** Reference policy names, let OPA define thresholds.

### ❌ Loading Environments When Not Needed

```csharp
// Inefficient
var app = await repository.GetByIdAsync(appId);
var hasProduction = app.Value.Environments.Any(e => e.Name == "production");
```

**Better:**
```csharp
var hasProduction = await repository.HasEnvironmentAsync(appId, "production");
```

### ❌ Tight Coupling to Security Tools

```csharp
// WRONG - hard-coded tool logic
public class EnvironmentConfig
{
    public string SnykProjectId { get; set; }
    public string PrismaCloudProjectId { get; set; }
    // What if we add Trivy? Modify domain model again?
}
```

**Correct:** Use Metadata dictionary for flexibility.

---

**Remember:** This context owns **what applications exist and how they're configured** - it does NOT own compliance evaluation logic (that's the Evaluation context).
