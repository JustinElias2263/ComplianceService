# Audit Bounded Context

**Maintains complete, immutable audit trail of all compliance decisions for regulatory compliance and security reporting.**

## Purpose

This bounded context is responsible for:
- Recording every compliance decision permanently
- Storing complete evidence (scan results, OPA input/output)
- Providing audit trail for regulatory compliance (SOC2, ISO 27001, etc.)
- Enabling compliance reporting and analysis
- Supporting incident investigation and forensics

**Key principles:**
- **Append-only**: Audit logs are never updated or deleted
- **Complete evidence**: Store all data needed to recreate decision
- **Fast queries**: Pre-calculated counts for reporting

## Domain Model

```
AuditLog (Aggregate Root - Immutable)
├── Properties
│   ├── Id: Guid
│   ├── EvaluationId: string (links to ComplianceEvaluation)
│   ├── ApplicationId: Guid
│   ├── ApplicationName: string (denormalized for historical accuracy)
│   ├── Environment: string
│   ├── RiskTier: string
│   ├── Allowed: bool (true = deployment allowed, false = blocked)
│   ├── Reason: string (human-readable summary)
│   ├── Violations: List<string> (policy violation messages)
│   ├── Evidence: DecisionEvidence (complete scan results + OPA I/O)
│   ├── EvaluationDurationMs: int
│   ├── EvaluatedAt: DateTime
│   │
│   └── Aggregated Counts (for fast querying)
│       ├── CriticalCount: int
│       ├── HighCount: int
│       ├── MediumCount: int
│       ├── LowCount: int
│       └── TotalVulnerabilityCount: int
│
└── Computed Properties
    ├── IsBlocked: bool
    ├── HasCriticalVulnerabilities: bool
    └── HasHighOrCriticalVulnerabilities: bool
```

### Value Objects

```
DecisionEvidence
├── ScanResultsJson: string (complete raw output from tools)
├── PolicyInputJson: string (what was sent to OPA)
├── PolicyOutputJson: string (what OPA returned)
└── CapturedAt: DateTime
```

## Ubiquitous Language

- **Audit Log**: Permanent record of a compliance decision
- **Evidence**: Complete data needed to recreate and verify a decision
- **Evaluation ID**: Unique identifier linking to ComplianceEvaluation
- **Denormalization**: Storing ApplicationName instead of just ID (historical accuracy)
- **Append-Only**: Records are created but never modified or deleted
- **Partition**: Time-based data organization (one partition per month)

## Components

### Aggregate Root: AuditLog

**File:** `AuditLog.cs`

**Responsibility:** Immutable record of a compliance decision.

**Factory Method:**
```csharp
public static Result<AuditLog> Create(
    string evaluationId,
    Guid applicationId,
    string applicationName,
    string environment,
    string riskTier,
    bool allowed,
    string reason,
    List<string> violations,
    DecisionEvidence evidence,
    int evaluationDurationMs,
    int criticalCount,
    int highCount,
    int mediumCount,
    int lowCount,
    DateTime evaluatedAt)
```

**Characteristics:**
- ✅ No update methods - completely immutable
- ✅ Stores denormalized data (ApplicationName) for historical accuracy
- ✅ Pre-calculates vulnerability counts for query performance
- ✅ Comprehensive evidence storage for compliance audits

**Computed Properties:**
```csharp
auditLog.IsBlocked                           // !Allowed
auditLog.HasCriticalVulnerabilities          // CriticalCount > 0
auditLog.HasHighOrCriticalVulnerabilities    // Critical or High > 0
```

### Value Object: DecisionEvidence

**File:** `ValueObjects/DecisionEvidence.cs`

**Purpose:** Complete evidence storage for compliance verification.

**Factory Method:**
```csharp
public static Result<DecisionEvidence> Create(
    string scanResultsJson,
    string policyInputJson,
    string policyOutputJson)
```

**What's Stored:**
- **ScanResultsJson**: Raw output from Snyk, Prisma Cloud, etc.
- **PolicyInputJson**: Exact payload sent to OPA for evaluation
- **PolicyOutputJson**: Complete OPA response with decision details

**Why store all this:**
- Regulatory compliance requires complete audit trail
- Enable recreating decision if needed
- Support dispute resolution ("why was this blocked?")
- Forensic analysis of security incidents

**Example:**
```csharp
var evidence = DecisionEvidence.Create(
    scanResultsJson: JsonSerializer.Serialize(scanResults),
    policyInputJson: JsonSerializer.Serialize(opaInput),
    policyOutputJson: JsonSerializer.Serialize(opaOutput)
).Value;
```

### Repository Interface

**File:** `Interfaces/IAuditLogRepository.cs`

**Key Methods:**
```csharp
// Retrieve specific audit log
Task<Result<AuditLog>> GetByIdAsync(Guid id);
Task<Result<AuditLog>> GetByEvaluationIdAsync(string evaluationId);

// Query by application
Task<IReadOnlyList<AuditLog>> GetByApplicationIdAsync(Guid applicationId, int pageSize, int pageNumber);
Task<IReadOnlyList<AuditLog>> GetByApplicationAndEnvironmentAsync(
    Guid applicationId, string environment, DateTime? fromDate, DateTime? toDate);

// Query blocked decisions
Task<IReadOnlyList<AuditLog>> GetBlockedDecisionsAsync(DateTime? since, int? limit);
Task<IReadOnlyList<AuditLog>> GetWithCriticalVulnerabilitiesAsync(DateTime? since);

// Query by risk tier
Task<IReadOnlyList<AuditLog>> GetByRiskTierAsync(string riskTier, DateTime? fromDate, DateTime? toDate);

// Statistics for reporting
Task<AuditStatistics> GetStatisticsAsync(DateTime? fromDate, DateTime? toDate);

// Add new audit log (no Update or Delete!)
Task<Result> AddAsync(AuditLog auditLog);
```

**AuditStatistics Record:**
```csharp
public record AuditStatistics(
    int TotalEvaluations,
    int AllowedCount,
    int BlockedCount,
    double BlockedPercentage,
    int TotalCriticalVulnerabilities,
    int TotalHighVulnerabilities,
    Dictionary<string, int> EvaluationsByEnvironment,
    Dictionary<string, int> EvaluationsByRiskTier);
```

## Use Cases

### 1. Create Audit Log from Evaluation

```csharp
// After ComplianceEvaluation is created
var evaluation = ComplianceEvaluation.Create(...).Value;

// Build evidence
var evidence = DecisionEvidence.Create(
    JsonSerializer.Serialize(scanResults),
    JsonSerializer.Serialize(opaInput),
    JsonSerializer.Serialize(opaResponse)
).Value;

// Create audit log
var auditLog = AuditLog.Create(
    evaluation.Id.ToString(),
    evaluation.ApplicationId,
    application.Name,  // Denormalized
    evaluation.Environment,
    evaluation.RiskTier,
    evaluation.Decision.Allowed,
    evaluation.Decision.GetReason(),
    evaluation.Decision.Violations,
    evidence,
    evaluation.Decision.EvaluationDurationMs,
    evaluation.GetCriticalVulnerabilityCount(),
    evaluation.GetHighVulnerabilityCount(),
    evaluation.GetMediumVulnerabilityCount(),
    evaluation.GetLowVulnerabilityCount(),
    evaluation.EvaluatedAt
).Value;

await auditRepository.AddAsync(auditLog);
```

### 2. Query Blocked Deployments

```csharp
// Find all blocked deployments in last 7 days
var blockedDeployments = await auditRepository.GetBlockedDecisionsAsync(
    since: DateTime.UtcNow.AddDays(-7),
    limit: 100
);

foreach (var log in blockedDeployments)
{
    Console.WriteLine($"{log.ApplicationName} - {log.Environment}");
    Console.WriteLine($"  Reason: {log.Reason}");
    Console.WriteLine($"  Violations: {string.Join(", ", log.Violations)}");
    Console.WriteLine($"  Critical: {log.CriticalCount}, High: {log.HighCount}");
}
```

### 3. Generate Compliance Report

```csharp
// Monthly compliance report
var stats = await auditRepository.GetStatisticsAsync(
    fromDate: new DateTime(2024, 1, 1),
    toDate: new DateTime(2024, 1, 31)
);

var report = new ComplianceReport
{
    TotalEvaluations = stats.TotalEvaluations,
    AllowedRate = (double)stats.AllowedCount / stats.TotalEvaluations,
    BlockedRate = stats.BlockedPercentage,
    TotalCriticalVulnerabilities = stats.TotalCriticalVulnerabilities,
    EvaluationsByEnvironment = stats.EvaluationsByEnvironment,
    MostRiskyApplications = await GetTopRiskyApplications(fromDate, toDate)
};
```

### 4. Investigate Specific Incident

```csharp
// Security team wants to review a blocked deployment
var auditLog = await auditRepository.GetByEvaluationIdAsync("eval-12345");

if (auditLog.IsSuccess)
{
    var log = auditLog.Value;

    // View decision details
    Console.WriteLine($"Allowed: {log.Allowed}");
    Console.WriteLine($"Reason: {log.Reason}");

    // Review evidence
    var scanResults = JsonSerializer.Deserialize<List<ScanResult>>(
        log.Evidence.ScanResultsJson);

    var opaInput = JsonSerializer.Deserialize<OpaInput>(
        log.Evidence.PolicyInputJson);

    var opaOutput = JsonSerializer.Deserialize<OpaOutput>(
        log.Evidence.PolicyOutputJson);

    // Can recreate entire decision process
}
```

## Development Guidelines

### Denormalization Strategy

**Why denormalize ApplicationName?**
- Application names can change over time
- Audit logs must reflect historical state
- Queries should show name at time of evaluation

**What to denormalize:**
- ✅ ApplicationName (can change)
- ✅ Environment (for fast querying)
- ✅ RiskTier (can change)
- ❌ Don't denormalize everything - balance query speed vs storage

**Example of denormalization value:**
```
// Application renamed from "payment-api" to "payment-service"
// Old audit logs still show "payment-api" (accurate historical record)
// New audit logs show "payment-service"
```

### Handling Large Evidence JSON

**Problem:** Scan results can be 100KB+ JSON

**Solutions:**

1. **Compress evidence before storage:**
```csharp
public static class EvidenceCompression
{
    public static string Compress(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(output.ToArray());
    }

    public static string Decompress(string compressed)
    {
        var bytes = Convert.FromBase64String(compressed);
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        return reader.ReadToEnd();
    }
}
```

2. **Store in blob storage for large scans:**
```csharp
public class DecisionEvidence : ValueObject
{
    public string? EvidenceBlobUrl { get; } // S3/Azure Blob URL
    public bool IsStoredExternally => !string.IsNullOrEmpty(EvidenceBlobUrl);
}
```

### Partitioning Strategy

**Scenario:** 1 million+ audit logs per year

**Solution:** Partition by month
```sql
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY,
    evaluation_id VARCHAR(255) UNIQUE,
    -- ... other fields
    created_at TIMESTAMP DEFAULT NOW()
) PARTITION BY RANGE (created_at);

-- Create partitions
CREATE TABLE audit_logs_2024_01 PARTITION OF audit_logs
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

CREATE TABLE audit_logs_2024_02 PARTITION OF audit_logs
    FOR VALUES FROM ('2024-02-01') TO ('2024-03-01');
```

**Benefits:**
- Query performance (scan only relevant partitions)
- Easy archival (detach old partitions)
- Maintenance (vacuum only active partitions)

### Archival Strategy

**Scenario:** Regulations require 7-year retention, but active queries only need 90 days.

**Strategy:**
1. **Hot storage** (0-90 days): Primary database, fast queries
2. **Warm storage** (91 days - 2 years): Read replicas, slower queries OK
3. **Cold storage** (2-7 years): Compressed files in S3/Azure Blob

**Implementation:**
```csharp
public interface IAuditLogRepository
{
    // Query hot storage
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int days = 90);

    // Query warm storage (slower)
    Task<IReadOnlyList<AuditLog>> GetArchivedAsync(DateTime fromDate, DateTime toDate);

    // Archive old logs
    Task ArchiveLogsAsync(DateTime olderThan);
}
```

## Testing

### Test Immutability

```csharp
[Fact]
public void AuditLog_ShouldBeImmutable()
{
    var evidence = DecisionEvidence.Create(...).Value;
    var auditLog = AuditLog.Create(..., evidence, ...).Value;

    // No public setters - this won't compile
    // auditLog.Allowed = true;
    // auditLog.Reason = "Changed";

    // Audit log is truly immutable
}
```

### Test Evidence Storage

```csharp
[Fact]
public void DecisionEvidence_ShouldStoreCompleteData()
{
    var scanResults = new List<ScanResult> { ... };
    var opaInput = new OpaInput { ... };
    var opaOutput = new OpaOutput { ... };

    var evidence = DecisionEvidence.Create(
        JsonSerializer.Serialize(scanResults),
        JsonSerializer.Serialize(opaInput),
        JsonSerializer.Serialize(opaOutput)
    ).Value;

    // Can deserialize back to original objects
    var deserializedScans = JsonSerializer.Deserialize<List<ScanResult>>(
        evidence.ScanResultsJson);

    deserializedScans.Should().BeEquivalentTo(scanResults);
}
```

### Test Computed Properties

```csharp
[Fact]
public void AuditLog_WithCriticalVulns_ShouldIndicateHasCritical()
{
    var auditLog = AuditLog.Create(
        ...,
        criticalCount: 2,
        highCount: 5,
        ...
    ).Value;

    auditLog.HasCriticalVulnerabilities.Should().BeTrue();
    auditLog.HasHighOrCriticalVulnerabilities.Should().BeTrue();
}

[Fact]
public void AuditLog_WhenBlocked_IsBlockedShouldBeTrue()
{
    var auditLog = AuditLog.Create(..., allowed: false, ...).Value;

    auditLog.IsBlocked.Should().BeTrue();
    auditLog.Allowed.Should().BeFalse();
}
```

## Scalability Considerations

### Query Performance

**Problem:** Queries slow with millions of audit logs

**Solutions:**

1. **Indexes:**
```sql
CREATE INDEX idx_audit_logs_app_id ON audit_logs(application_id);
CREATE INDEX idx_audit_logs_evaluated_at ON audit_logs(evaluated_at);
CREATE INDEX idx_audit_logs_allowed ON audit_logs(allowed);
CREATE INDEX idx_audit_logs_risk_tier ON audit_logs(risk_tier);

-- Composite index for common query
CREATE INDEX idx_audit_logs_app_env_date
    ON audit_logs(application_id, environment, evaluated_at);
```

2. **Materialized views for statistics:**
```sql
CREATE MATERIALIZED VIEW audit_statistics_daily AS
SELECT
    DATE(evaluated_at) as date,
    COUNT(*) as total_evaluations,
    SUM(CASE WHEN allowed THEN 1 ELSE 0 END) as allowed_count,
    SUM(CASE WHEN NOT allowed THEN 1 ELSE 0 END) as blocked_count,
    SUM(critical_count) as total_critical_vulns
FROM audit_logs
GROUP BY DATE(evaluated_at);

-- Refresh daily
REFRESH MATERIALIZED VIEW audit_statistics_daily;
```

3. **Read replicas:**
- Primary database handles writes (fast, no reads)
- Read replicas handle queries (can lag by seconds)

### Storage Growth

**Problem:** 100GB+ of audit logs per year

**Solutions:**

1. **Compression:**
   - Compress evidence JSON (50-70% size reduction)
   - Use PostgreSQL compression (TOAST)

2. **Tiered storage:**
   - Recent logs: SSD storage
   - Old logs: HDD storage or object storage

3. **Archival:**
   - Export old partitions to Parquet files
   - Store in S3/Azure Blob with Glacier/Archive tier
   - Keep index in database with blob references

## Integration with Other Contexts

### Evaluation Context
- ComplianceEvaluation triggers AuditLog creation
- Domain event `ComplianceEvaluationCompletedEvent` → Create AuditLog
- Link via EvaluationId

### Application Context
- Stores ApplicationId and ApplicationName (denormalized)
- No writes to Application - read-only reference
- ApplicationName at evaluation time preserved forever

## Regulatory Compliance

### SOC 2 Type II
- **AU-2**: Audit events - ✅ All decisions logged
- **AU-3**: Content of audit records - ✅ Complete evidence stored
- **AU-9**: Protection of audit information - ✅ Append-only, immutable
- **AU-11**: Audit record retention - ✅ 7-year retention supported

### ISO 27001
- **A.12.4.1**: Event logging - ✅ All security decisions logged
- **A.12.4.2**: Protection of log information - ✅ Immutable records
- **A.12.4.3**: Administrator and operator logs - ✅ Evaluation details stored
- **A.12.4.4**: Clock synchronization - ✅ UTC timestamps

### GDPR
- **Article 30**: Records of processing - ✅ Complete audit trail
- **Right to erasure**: If personal data in scan results, implement anonymization for old logs

## Common Pitfalls

### ❌ Updating Audit Logs

```csharp
// WRONG - audit logs are immutable
public void CorrectReason(string newReason)
{
    Reason = newReason; // Never modify audit logs!
}
```

**Why wrong:** Violates audit integrity. If mistake, create compensating entry.

### ❌ Storing Sensitive Data

```csharp
// WRONG
var evidence = DecisionEvidence.Create(
    scanResults: jsonWithApiKeys,  // Contains secrets!
    ...
);
```

**Why wrong:** Audit logs may be exported for compliance. Sanitize secrets first.

### ❌ Not Denormalizing Enough

```csharp
// WRONG - only storing ID
public class AuditLog
{
    public Guid ApplicationId { get; set; }
    // Missing ApplicationName - can't show historical name
}
```

**Why wrong:** If application renamed, can't show name at evaluation time.

### ❌ Querying Without Indexes

```csharp
// WRONG - full table scan
var logs = await context.AuditLogs
    .Where(l => l.ApplicationId == appId && l.EvaluatedAt > date)
    .ToListAsync();
```

**Fix:** Ensure composite index on (application_id, evaluated_at).

---

**Remember:** Audit logs are **permanent historical records** for compliance. Design for immutability, long-term storage, and regulatory requirements.
