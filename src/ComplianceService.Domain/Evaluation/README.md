# Evaluation Bounded Context

**Handles compliance evaluation of security scan results against OPA policies.**

## Purpose

This bounded context is responsible for:
- Accepting security scan results from CI pipelines
- Aggregating vulnerabilities across multiple security tools
- Coordinating with OPA for policy evaluation
- Producing pass/fail deployment decisions
- Raising domain events for audit trail

**Key principle:** Evaluation is **stateless and immutable** - once created, the evaluation result never changes.

## Domain Model

```
ComplianceEvaluation (Aggregate Root)
├── Properties
│   ├── Id: Guid
│   ├── ApplicationId: Guid
│   ├── Environment: string
│   ├── RiskTier: string
│   ├── ScanResults: List<ScanResult> (value objects)
│   ├── Decision: PolicyDecision (value object)
│   └── EvaluatedAt: DateTime
│
└── Methods
    ├── GetCriticalVulnerabilityCount(): int
    ├── GetHighVulnerabilityCount(): int
    ├── GetMediumVulnerabilityCount(): int
    ├── GetLowVulnerabilityCount(): int
    └── GetTotalVulnerabilityCount(): int
```

### Value Objects

```
ScanResult
├── Tool: string (snyk, prismacloud)
├── ToolVersion: string
├── ScanDate: DateTime
├── ProjectId: string?
└── Vulnerabilities: List<Vulnerability>

Vulnerability
├── Id: string (CVE-2024-1234, SNYK-JS-...)
├── Title: string
├── Severity: VulnerabilitySeverity
├── CvssScore: double (0-10)
├── PackageName: string
├── PackageVersion: string
├── FixedIn: string?
├── Cve: List<string>
└── Cwe: List<string>

PolicyDecision
├── Allowed: bool
├── Violations: List<string>
├── Details: Dictionary<string, object>
└── EvaluationDurationMs: int
```

## Ubiquitous Language

- **Evaluation**: Complete assessment of scan results against policies
- **Scan Result**: Output from a security tool (Snyk, Prisma Cloud)
- **Vulnerability**: Security weakness found by scanner (CVE, SNYK-ID, etc.)
- **Severity**: Impact level (critical, high, medium, low) based on CVSS
- **Policy Decision**: OPA's determination whether deployment is allowed
- **Violation**: Human-readable reason why deployment was blocked
- **CVSS Score**: Common Vulnerability Scoring System (0.0-10.0)

## Components

### Aggregate Root: ComplianceEvaluation

**File:** `ComplianceEvaluation.cs`

**Responsibility:** Immutable record of a compliance evaluation.

**Key Method:**
```csharp
public static Result<ComplianceEvaluation> Create(
    Guid applicationId,
    string environment,
    string riskTier,
    List<ScanResult> scanResults,
    PolicyDecision decision)
```

**Invariants:**
- ✅ At least one scan result required
- ✅ ApplicationId cannot be empty
- ✅ Environment and RiskTier must be specified
- ✅ PolicyDecision is required
- ✅ Evaluation is immutable after creation

**Domain Events:**
- `ComplianceEvaluationCompletedEvent` - Published immediately after creation

**Aggregated Counts:**
```csharp
evaluation.GetCriticalVulnerabilityCount() // Sum across all tools
evaluation.GetHighVulnerabilityCount()
evaluation.GetMediumVulnerabilityCount()
evaluation.GetLowVulnerabilityCount()
evaluation.GetTotalVulnerabilityCount()
```

### Value Object: ScanResult

**File:** `ValueObjects/ScanResult.cs`

**Purpose:** Container for one security tool's scan output.

**Factory Method:**
```csharp
public static Result<ScanResult> Create(
    string tool,
    string toolVersion,
    DateTime scanDate,
    List<Vulnerability> vulnerabilities,
    string? projectId = null)
```

**Validation:**
- Tool name and version required
- Scan date cannot be in future
- Empty vulnerability list is allowed (no vulns found)

**Calculated Properties:**
```csharp
scanResult.CriticalCount  // Count of critical vulnerabilities
scanResult.HighCount
scanResult.MediumCount
scanResult.LowCount
scanResult.TotalCount
```

**Usage Example:**
```csharp
var vulnerabilities = new List<Vulnerability> { ... };
var scanResult = ScanResult.Create(
    "snyk",
    "1.1200.0",
    DateTime.UtcNow,
    vulnerabilities,
    "project-abc-123"
);
```

### Value Object: Vulnerability

**File:** `ValueObjects/Vulnerability.cs`

**Purpose:** Individual security vulnerability found by scanner.

**Factory Method:**
```csharp
public static Result<Vulnerability> Create(
    string id,              // CVE-2024-1234 or SNYK-JS-AXIOS-123
    string title,
    string severity,        // critical, high, medium, low
    double cvssScore,       // 0.0-10.0
    string packageName,     // axios, lodash, nginx
    string packageVersion,  // 0.21.0
    string? fixedIn = null,
    List<string>? cve = null,
    List<string>? cwe = null)
```

**Validation:**
- ID and title required
- Severity must be valid (critical/high/medium/low)
- CVSS score must be 0-10
- Package name and version required

**Properties:**
```csharp
vuln.IsCritical        // severity == "critical"
vuln.IsHighOrAbove     // severity is "critical" or "high"
vuln.CvssScore         // 0.0-10.0
vuln.Cve               // List of CVE IDs
vuln.Cwe               // List of CWE IDs (weakness types)
```

### Value Object: VulnerabilitySeverity

**File:** `ValueObjects/VulnerabilitySeverity.cs`

**Purpose:** Type-safe severity levels.

**Values:**
```csharp
VulnerabilitySeverity.Critical  // CVSS 9.0-10.0
VulnerabilitySeverity.High      // CVSS 7.0-8.9
VulnerabilitySeverity.Medium    // CVSS 4.0-6.9
VulnerabilitySeverity.Low       // CVSS 0.1-3.9
```

**Helper Methods:**
```csharp
severity.IsCritical             // true if critical
severity.IsHighOrAbove          // true if critical or high
severity.IsMediumOrAbove        // true if critical, high, or medium
```

### Value Object: PolicyDecision

**File:** `ValueObjects/PolicyDecision.cs`

**Purpose:** OPA policy evaluation result.

**Factory Method:**
```csharp
public static Result<PolicyDecision> Create(
    bool allowed,
    List<string>? violations = null,
    Dictionary<string, object>? details = null,
    int evaluationDurationMs = 0)
```

**Validation:**
- If not allowed, must have at least one violation
- Evaluation duration cannot be negative

**Properties:**
```csharp
decision.Allowed                        // true = deploy, false = block
decision.Violations                     // Human-readable reasons
decision.Details                        // Structured data from OPA
decision.EvaluationDurationMs          // OPA evaluation time
decision.GetReason()                    // Formatted reason string
```

**Example:**
```csharp
var decision = PolicyDecision.Create(
    allowed: false,
    violations: new List<string> {
        "Critical vulnerabilities (2) exceed maximum (0)",
        "High vulnerabilities (5) exceed maximum (0)"
    },
    details: new Dictionary<string, object> {
        ["criticalCount"] = 2,
        ["highCount"] = 5,
        ["thresholds"] = new { critical = 0, high = 0 }
    },
    evaluationDurationMs: 4
);
```

### Repository Interface

**File:** `Interfaces/IComplianceEvaluationRepository.cs`

**Key Methods:**
```csharp
Task<Result<ComplianceEvaluation>> GetByIdAsync(Guid id);
Task<IReadOnlyList<ComplianceEvaluation>> GetByApplicationIdAsync(Guid applicationId);
Task<IReadOnlyList<ComplianceEvaluation>> GetByApplicationAndEnvironmentAsync(
    Guid applicationId, string environment);
Task<IReadOnlyList<ComplianceEvaluation>> GetRecentAsync(int days = 7);
Task<IReadOnlyList<ComplianceEvaluation>> GetBlockedEvaluationsAsync(DateTime? since = null);
Task<Result> AddAsync(ComplianceEvaluation evaluation);
```

## Use Cases

### 1. Evaluate Scan Results

**Scenario:** CI pipeline sends Snyk and Prisma Cloud results for evaluation.

```csharp
// 1. Parse vulnerabilities from tool outputs
var snykVulns = ParseSnykOutput(snykJson);
var prismaVulns = ParsePrismaOutput(prismaJson);

// 2. Create scan results
var snykScan = ScanResult.Create("snyk", "1.1200.0", DateTime.UtcNow, snykVulns).Value;
var prismaScan = ScanResult.Create("prismacloud", "22.12.415", DateTime.UtcNow, prismaVulns).Value;

// 3. Get application profile (from Application context)
var application = await appRepository.GetByIdAsync(applicationId);
var environment = application.Value.GetEnvironment("production");

// 4. Build OPA input and query
var opaInput = BuildOpaInput(application.Value, environment.Value, new[] { snykScan, prismaScan });
var opaResponse = await opaClient.EvaluateAsync(opaInput);

// 5. Create policy decision
var decision = PolicyDecision.Create(
    opaResponse.Allowed,
    opaResponse.Violations,
    opaResponse.Details,
    opaResponse.EvaluationDurationMs
).Value;

// 6. Create evaluation
var evaluation = ComplianceEvaluation.Create(
    applicationId,
    "production",
    application.Value.RiskTier.Value,
    new List<ScanResult> { snykScan, prismaScan },
    decision
).Value;

// 7. Save evaluation
await evaluationRepository.AddAsync(evaluation);

// 8. Return result to CI pipeline
return new EvaluationResponse
{
    Allowed = evaluation.IsAllowed,
    Reason = decision.GetReason(),
    EvaluationId = evaluation.Id.ToString()
};
```

### 2. Query Recent Blocked Evaluations

```csharp
var blockedEvaluations = await repository.GetBlockedEvaluationsAsync(
    since: DateTime.UtcNow.AddDays(-7)
);

foreach (var eval in blockedEvaluations)
{
    Console.WriteLine($"{eval.ApplicationId}: {eval.Decision.GetReason()}");
    Console.WriteLine($"  Critical: {eval.GetCriticalVulnerabilityCount()}");
    Console.WriteLine($"  High: {eval.GetHighVulnerabilityCount()}");
}
```

### 3. Aggregate Vulnerability Statistics

```csharp
var evaluations = await repository.GetByApplicationAndEnvironmentAsync(
    applicationId,
    "production"
);

var totalCritical = evaluations.Sum(e => e.GetCriticalVulnerabilityCount());
var totalHigh = evaluations.Sum(e => e.GetHighVulnerabilityCount());
var blockRate = evaluations.Count(e => e.IsBlocked) / (double)evaluations.Count;

Console.WriteLine($"Production deployment block rate: {blockRate:P}");
Console.WriteLine($"Average critical vulnerabilities: {totalCritical / evaluations.Count}");
```

## Development Guidelines

### Adding New Security Tool

**Example:** Adding Trivy scanner support

**Steps:**
1. No domain model changes needed! ScanResult accepts any tool name.
2. Update tool parsing logic in Application layer
3. Update OPA policies to handle Trivy-specific vulnerability format (if different)
4. Update API to accept Trivy results

**That's it - domain is already extensible.**

### Adding Vulnerability Property

**Example:** Adding "exploitability" score

```csharp
public sealed class Vulnerability : ValueObject
{
    public string Id { get; }
    // ... existing properties
    public double? ExploitabilityScore { get; } // NEW

    private Vulnerability(..., double? exploitabilityScore)
    {
        // ... existing initialization
        ExploitabilityScore = exploitabilityScore;
    }

    public static Result<Vulnerability> Create(
        ...,
        double? exploitabilityScore = null) // NEW parameter
    {
        // ... existing validation

        return Result.Success(new Vulnerability(..., exploitabilityScore));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
        yield return PackageName;
        yield return PackageVersion;
        // Don't include ExploitabilityScore in equality - optional metadata
    }
}
```

### Custom Vulnerability Severity Mapping

**Scenario:** Different tools use different severity scales.

```csharp
public static class SeverityMapper
{
    public static string MapPrismaCloudSeverity(string prismaSeverity)
    {
        // Prisma Cloud uses: "important" instead of "high"
        return prismaSeverity.ToLowerInvariant() switch
        {
            "critical" => "critical",
            "important" => "high",
            "moderate" => "medium",
            "low" => "low",
            _ => "medium" // Default to medium if unknown
        };
    }

    public static string MapSnykSeverity(string snykSeverity)
    {
        // Snyk already uses standard severity levels
        return snykSeverity.ToLowerInvariant();
    }
}
```

## Testing

### Test Vulnerability Aggregation

```csharp
[Fact]
public void Evaluation_WithMultipleTools_ShouldAggregateVulnerabilities()
{
    var snykVulns = new List<Vulnerability> {
        CreateCriticalVuln(),
        CreateHighVuln()
    };
    var prismaVulns = new List<Vulnerability> {
        CreateCriticalVuln()
    };

    var snykScan = ScanResult.Create("snyk", "1.0", DateTime.UtcNow, snykVulns).Value;
    var prismaScan = ScanResult.Create("prismacloud", "1.0", DateTime.UtcNow, prismaVulns).Value;

    var decision = PolicyDecision.Create(false, new List<string> { "Too many vulns" }).Value;
    var evaluation = ComplianceEvaluation.Create(
        Guid.NewGuid(),
        "production",
        "critical",
        new List<ScanResult> { snykScan, prismaScan },
        decision
    ).Value;

    evaluation.GetCriticalVulnerabilityCount().Should().Be(2); // 1 from Snyk + 1 from Prisma
    evaluation.GetHighVulnerabilityCount().Should().Be(1);
    evaluation.GetTotalVulnerabilityCount().Should().Be(3);
}
```

### Test Policy Decision Validation

```csharp
[Fact]
public void PolicyDecision_WhenDenied_MustHaveViolations()
{
    var result = PolicyDecision.Create(allowed: false, violations: new List<string>());

    result.IsFailure.Should().BeTrue();
    result.Error.Should().Contain("violation");
}

[Fact]
public void PolicyDecision_WhenAllowed_CanHaveEmptyViolations()
{
    var result = PolicyDecision.Create(allowed: true, violations: new List<string>());

    result.IsSuccess.Should().BeTrue();
}
```

## Scalability Considerations

### High Evaluation Volume

**Scenario:** 10,000 evaluations per day

**Solutions:**
1. **Partitioned storage**: Partition by date (one table per month)
2. **Hot/cold storage**: Move old evaluations to archive after 90 days
3. **Read replicas**: Query old evaluations from read-only replica
4. **Indexing**: Index on (ApplicationId, Environment, EvaluatedAt)

### Large Scan Results

**Scenario:** Scan has 1,000+ vulnerabilities

**Current design handles well:**
- Vulnerabilities are value objects (no extra DB queries)
- Counts calculated in-memory
- Only store what's needed for compliance decision

**If JSON storage becomes issue:**
- Store top N critical/high vulnerabilities only
- Store full results in blob storage (S3, Azure Blob)
- Reference blob URL in evaluation

### Policy Decision Details

**PolicyDecision.Details can contain arbitrary data from OPA.**

**Best practices:**
- Keep details small (<10KB JSON)
- Store only data needed for audit/reporting
- Don't return raw scan results in details (already in ScanResults)

## Integration with Other Contexts

### Application Context
- Reads ApplicationProfile to get RiskTier
- Reads EnvironmentConfig to get policies to evaluate
- No write operations - read-only dependency

### Audit Context
- Evaluation triggers creation of AuditLog
- Domain event `ComplianceEvaluationCompletedEvent` handled by audit service
- Evaluation ID links to audit log

## Common Pitfalls

### ❌ Modifying Evaluation After Creation

```csharp
// WRONG - evaluation should be immutable
public void UpdateDecision(PolicyDecision newDecision)
{
    Decision = newDecision; // NO!
}
```

**Why wrong:** Evaluations are historical records. Create new evaluation instead.

### ❌ Storing Tool API Credentials

```csharp
// WRONG
public class ScanResult
{
    public string ApiKey { get; set; } // Never store credentials
}
```

**Why wrong:** Domain objects may be logged/serialized. CI pipeline manages credentials.

### ❌ Business Logic in Value Objects

```csharp
// WRONG
public class Vulnerability
{
    public bool ShouldBlockDeployment()
    {
        return IsCritical && IsProduction; // Business rule in value object
    }
}
```

**Why wrong:** This is policy logic - belongs in OPA policies, not domain.

---

**Remember:** Evaluation context is **stateless and immutable** - it records what happened, it doesn't manage state over time.
