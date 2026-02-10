# ComplianceService.Infrastructure

## Overview

The **Infrastructure** layer implements external concerns and technical details for the ComplianceService application. It provides concrete implementations of repository interfaces, database persistence with Entity Framework Core, and integration with external systems like Open Policy Agent (OPA).

This layer follows the **Dependency Inversion Principle** - it depends on abstractions defined in the Domain and Application layers, not the other way around.

## Architecture Principles

- **Clean Architecture**: Infrastructure is the outermost layer, depending on Application and Domain
- **Dependency Inversion**: Implements interfaces defined in inner layers
- **Persistence Ignorance**: Domain models are not coupled to database concerns
- **External Service Integration**: HTTP clients, notification services, and third-party integrations

## Technology Stack

- **.NET 8.0**: Framework
- **Entity Framework Core 8.0**: ORM for PostgreSQL
- **Npgsql**: PostgreSQL provider for EF Core
- **HttpClient**: For OPA sidecar communication
- **Microsoft.Extensions.Logging**: Structured logging

## Project Structure

```
ComplianceService.Infrastructure/
├── Persistence/
│   ├── ApplicationDbContext.cs           # EF Core DbContext
│   ├── Configurations/                   # Entity type configurations
│   │   ├── ApplicationConfiguration.cs
│   │   ├── EnvironmentConfigConfiguration.cs
│   │   ├── ComplianceEvaluationConfiguration.cs
│   │   └── AuditLogConfiguration.cs
│   ├── Repositories/                     # Repository implementations
│   │   ├── ApplicationRepository.cs
│   │   ├── ComplianceEvaluationRepository.cs
│   │   └── AuditLogRepository.cs
│   └── Migrations/                       # EF Core migrations
│       └── 20260210_InitialCreate.cs
├── ExternalServices/                     # External integrations
│   ├── OpaHttpClient.cs                  # OPA HTTP client
│   └── LoggingNotificationService.cs     # Notification service
└── DependencyInjection.cs                # Service registration
```

## Database Context

### ApplicationDbContext

The `ApplicationDbContext` manages all database operations for the Compliance Service.

**Features:**
- PostgreSQL with `compliance` schema
- Supports three aggregate roots: `Application`, `ComplianceEvaluation`, `AuditLog`
- Fluent API entity configurations
- JSONB columns for complex value objects
- Optimized indexes for query performance

**Configuration:**
```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
        .EnableRetryOnFailure(maxRetryCount: 3));
```

## Entity Configurations

### ApplicationConfiguration

Maps the `Application` aggregate root to the `Applications` table.

**Key Features:**
- Unique index on `Name`
- Index on `Owner` for filtering
- Cascade delete for `EnvironmentConfigs`
- Owned entity mapping for environment configurations

### EnvironmentConfigConfiguration

Maps the `EnvironmentConfig` entity (owned by Application).

**Key Features:**
- Value object conversion for `RiskTier`
- JSON storage for `SecurityTools` and `PolicyReferences` collections
- JSONB for `Metadata` dictionary
- Composite unique index on `(ApplicationId, Name)`

**Example:**
```csharp
// RiskTier value object conversion
builder.Property(e => e.RiskTier)
    .HasConversion(
        v => v.Value,
        v => RiskTier.FromString(v).Value);

// JSON storage for collections
builder.Property<string>("_securityToolsJson")
    .HasColumnName("SecurityTools")
    .HasColumnType("jsonb");
```

### ComplianceEvaluationConfiguration

Maps the `ComplianceEvaluation` aggregate root.

**Key Features:**
- JSONB for `ScanResults` and `PolicyDecision`
- Composite index on `(ApplicationId, Environment, EvaluatedAt)`
- Supports time-based queries for recent evaluations

### AuditLogConfiguration

Maps the `AuditLog` aggregate root for immutable audit trail.

**Key Features:**
- JSONB for `Evidence` and `Violations`
- Multiple indexes for audit queries
- Table partitioning hint for scalability (monthly partitions)
- Unique index on `EvaluationId`

**Indexes:**
- `ApplicationName`, `Environment`, `EvaluatedAt`, `RiskTier`, `Allowed`
- Composite index on `(ApplicationName, Environment, EvaluatedAt)`

## Repositories

All repositories follow the **Repository Pattern** and implement interfaces from the Domain layer.

### ApplicationRepository

Implements `IApplicationRepository` for application registration and management.

**Methods:**
- `GetByIdAsync(Guid)` - Get application by ID with environments
- `GetByNameAsync(string)` - Get application by name
- `GetAllAsync()` - Get all applications ordered by name
- `GetByOwnerAsync(string)` - Get applications by owner
- `GetActiveApplicationsAsync()` - Get active applications only
- `AddAsync(Application)` - Add new application
- `UpdateAsync(Application)` - Update existing application
- `DeleteAsync(Application)` - Delete application
- `ExistsAsync(Guid)` - Check if application exists
- `NameExistsAsync(string, Guid?)` - Check if name is taken
- `GetTotalCountAsync()` - Get total count
- `SaveChangesAsync()` - Commit transaction

**Example:**
```csharp
var application = await _applicationRepository.GetByIdAsync(applicationId);
if (application.IsSuccess)
{
    application.Value.UpdateOwner("new-owner@example.com");
    await _applicationRepository.UpdateAsync(application.Value);
    await _applicationRepository.SaveChangesAsync();
}
```

### ComplianceEvaluationRepository

Implements `IComplianceEvaluationRepository` for evaluation history.

**Methods:**
- `GetByIdAsync(Guid)` - Get evaluation by ID
- `GetByApplicationIdAsync(Guid)` - Get all evaluations for application
- `GetByApplicationAndEnvironmentAsync(Guid, string)` - Filter by environment
- `GetRecentAsync(int days)` - Get recent evaluations (default 7 days)
- `GetBlockedEvaluationsAsync(DateTime?)` - Get failed evaluations
- `AddAsync(ComplianceEvaluation)` - Add new evaluation
- `SaveChangesAsync()` - Commit transaction

**Example:**
```csharp
var recentEvaluations = await _evaluationRepository.GetRecentAsync(days: 30);
var blockedEvaluations = await _evaluationRepository.GetBlockedEvaluationsAsync(
    since: DateTime.UtcNow.AddDays(-7));
```

### AuditLogRepository

Implements `IAuditLogRepository` for immutable audit trail.

**Methods:**
- `GetByIdAsync(Guid)` - Get audit log by ID
- `GetByEvaluationIdAsync(string)` - Get audit log by evaluation ID
- `GetByApplicationIdAsync(Guid, pageSize, pageNumber)` - Paginated query
- `GetByApplicationAndEnvironmentAsync(Guid, string, fromDate?, toDate?)` - Date range query
- `GetBlockedDecisionsAsync(DateTime?, int?)` - Get denied deployments
- `GetWithCriticalVulnerabilitiesAsync(DateTime?)` - Critical vulnerability audit
- `GetByRiskTierAsync(string, fromDate?, toDate?)` - Filter by risk tier
- `GetStatisticsAsync(fromDate?, toDate?)` - Aggregate statistics
- `AddAsync(AuditLog)` - Append to audit log (immutable)
- `SaveChangesAsync()` - Commit transaction

**Example:**
```csharp
// Get audit statistics
var stats = await _auditLogRepository.GetStatisticsAsync(
    fromDate: DateTime.UtcNow.AddMonths(-1),
    toDate: DateTime.UtcNow);

Console.WriteLine($"Total: {stats.TotalEvaluations}");
Console.WriteLine($"Blocked: {stats.BlockedCount} ({stats.BlockedPercentage:F2}%)");
Console.WriteLine($"Critical Vulnerabilities: {stats.TotalCriticalVulnerabilities}");
```

## External Services

### OpaHttpClient

HTTP client for communicating with Open Policy Agent (OPA) sidecar.

**Features:**
- Sends policy evaluation requests to OPA
- Parses OPA responses into `OpaDecisionDto`
- Health check endpoint for readiness probes
- Configurable timeout and base URL
- Structured logging for observability

**Configuration:**
```json
{
  "OpaSettings": {
    "BaseUrl": "http://localhost:8181",
    "TimeoutSeconds": 30
  }
}
```

**Example:**
```csharp
var decision = await _opaClient.EvaluatePolicyAsync(
    input: opaInput,
    policyPackage: "compliance.cicd.production",
    cancellationToken: cancellationToken);

if (!decision.Allow)
{
    // Handle policy violations
    foreach (var violation in decision.Violations)
    {
        Console.WriteLine($"[{violation.Severity}] {violation.Rule}: {violation.Message}");
    }
}
```

**OPA Request Format:**
```json
POST /v1/data/compliance/cicd/production
{
  "input": {
    "application": {
      "name": "my-service",
      "environment": "production",
      "riskTier": "critical",
      "owner": "team@example.com"
    },
    "scanResults": [
      {
        "tool": "snyk",
        "criticalCount": 0,
        "highCount": 2,
        "vulnerabilities": [...]
      }
    ]
  }
}
```

**OPA Response Format:**
```json
{
  "result": {
    "allow": false,
    "violations": [
      {
        "rule": "no_critical_vulnerabilities",
        "message": "Production deployments must have zero critical vulnerabilities",
        "severity": "critical"
      }
    ],
    "reason": "Policy violations detected"
  }
}
```

### LoggingNotificationService

Logging-based implementation of `INotificationService`.

**Features:**
- Structured logging for compliance notifications
- Critical vulnerability alerts
- Extensible for email, Slack, PagerDuty, etc.

**Methods:**
- `SendComplianceNotificationAsync()` - Notify on evaluation completion
- `SendCriticalVulnerabilityAlertAsync()` - Alert on critical vulnerabilities

**Example:**
```csharp
await _notificationService.SendComplianceNotificationAsync(
    applicationName: "my-service",
    environment: "production",
    passed: false,
    violations: new[] { "Critical vulnerability detected" },
    recipients: new[] { "team@example.com" });
```

**Extension Points:**
```csharp
// TODO: Extend LoggingNotificationService for:
// - Email (SMTP, SendGrid)
// - Slack webhooks
// - Microsoft Teams
// - PagerDuty for on-call escalation
// - Custom webhooks
```

## Dependency Injection

### Registration

The `DependencyInjection.cs` class provides extension methods for service registration.

**Usage:**
```csharp
// In Program.cs or Startup.cs
services.AddInfrastructure(configuration);
```

**Registered Services:**
- `ApplicationDbContext` - Scoped (per request)
- `IApplicationRepository` → `ApplicationRepository` - Scoped
- `IComplianceEvaluationRepository` → `ComplianceEvaluationRepository` - Scoped
- `IAuditLogRepository` → `AuditLogRepository` - Scoped
- `IOpaClient` → `OpaHttpClient` - HttpClient factory (Transient)
- `INotificationService` → `LoggingNotificationService` - Scoped

### Database Configuration

**Connection String:**
```json
{
  "ConnectionStrings": {
    "ComplianceDatabase": "Host=localhost;Port=5432;Database=compliance_service;Username=postgres;Password=***"
  }
}
```

**Features:**
- Retry on failure (max 3 retries, 5-second delay)
- Sensitive data logging (development only)
- Detailed errors (development only)
- Migrations assembly configured

### Migration Helper

```csharp
// Apply migrations on startup (development/staging only)
DependencyInjection.ApplyMigrations(serviceProvider);
```

**WARNING:** Do not use automatic migrations in production. Use explicit migration scripts instead.

## Database Migrations

### Initial Migration

The `20260210_InitialCreate` migration creates the initial database schema.

**Created Objects:**
- Schema: `compliance`
- Tables: `Applications`, `EnvironmentConfigs`, `ComplianceEvaluations`, `AuditLogs`
- Indexes: Unique, composite, and single-column indexes
- Foreign keys: Cascade delete for environment configs

### Running Migrations

**Using EF Core CLI:**
```bash
# Add migration
dotnet ef migrations add MigrationName --project ComplianceService.Infrastructure

# Update database
dotnet ef database update --project ComplianceService.Infrastructure

# Generate SQL script
dotnet ef migrations script --project ComplianceService.Infrastructure --output migration.sql
```

**Automatic Migration (Development):**
```csharp
DependencyInjection.ApplyMigrations(app.Services);
```

**Production Migration:**
```bash
# Generate migration SQL
dotnet ef migrations script --idempotent --output migration.sql

# Review and apply SQL manually or via CI/CD pipeline
psql -U postgres -d compliance_service -f migration.sql
```

## PostgreSQL Features

### JSONB Storage

The infrastructure layer leverages PostgreSQL's JSONB data type for:
- `SecurityTools` and `PolicyReferences` (arrays)
- `Metadata` (dictionary)
- `ScanResults` and `PolicyDecision` (complex objects)
- `Evidence` (complete audit evidence)

**Benefits:**
- Flexible schema for complex value objects
- Efficient indexing and querying
- Native JSON operators in PostgreSQL

**Example Query:**
```sql
-- Find applications with Snyk configured
SELECT * FROM compliance."EnvironmentConfigs"
WHERE "SecurityTools" @> '["snyk"]'::jsonb;

-- Find evaluations with critical vulnerabilities
SELECT * FROM compliance."AuditLogs"
WHERE "Evidence"->'vulnerabilities' @> '[{"severity": "critical"}]'::jsonb;
```

### Table Partitioning

The `AuditLogs` table includes a partitioning hint for scalability:
```sql
-- Partition by month (implemented at database level)
CREATE TABLE compliance."AuditLogs_2026_02" PARTITION OF compliance."AuditLogs"
FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
```

**Benefits:**
- Improved query performance for time-range queries
- Easier data archival and retention policies
- Better index management

### Indexes

**Optimized for:**
- Application lookup by name (unique)
- Environment-specific queries
- Time-range audit queries
- Risk tier filtering
- Blocked decision reports

## Performance Considerations

### Connection Pooling

EF Core with Npgsql uses connection pooling by default:
```
Min Pool Size=5; Max Pool Size=100; Pooling=true
```

### Query Optimization

- **Include Statements**: Eager load related entities to avoid N+1 queries
- **AsNoTracking**: Use for read-only queries to improve performance
- **Pagination**: Implement `Skip/Take` for large result sets
- **Indexes**: Strategic indexes on foreign keys and query predicates

### Caching Strategy

Consider adding:
- **Response caching** for GET endpoints
- **Distributed cache** (Redis) for application configurations
- **Memory cache** for frequently accessed reference data

## Testing

### Unit Tests

Test repositories with in-memory database:
```csharp
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: "TestDb")
    .Options;

var context = new ApplicationDbContext(options);
var repository = new ApplicationRepository(context);
```

### Integration Tests

Test with PostgreSQL container:
```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql("Host=localhost;Port=5433;Database=test_db"));
```

**Recommended Tools:**
- Testcontainers for PostgreSQL
- Respawn for database cleanup
- FluentAssertions for assertions

## Logging and Observability

### Structured Logging

All Infrastructure services use structured logging:
```csharp
_logger.LogInformation(
    "Policy evaluation completed: Allow={Allow}, Violations={ViolationCount}",
    decision.Allow,
    decision.Violations.Count);
```

### Metrics (Future)

Consider instrumenting:
- Database query duration
- OPA evaluation latency
- Repository operation counts
- Notification delivery success rate

### Health Checks

```csharp
services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddUrlGroup(new Uri("http://localhost:8181/health"), "OPA");
```

## Security Considerations

### Connection Strings

- **Never commit** connection strings with credentials
- Use **environment variables** or **Azure Key Vault**
- Enable **SSL/TLS** for database connections in production

### Sensitive Data

- Disable `EnableSensitiveDataLogging` in production
- Use parameterized queries (EF Core does this by default)
- Audit log contains complete evidence - ensure proper access control

### OPA Communication

- Use **mTLS** for OPA sidecar communication in production
- Validate OPA responses to prevent injection attacks
- Set appropriate **timeouts** to prevent hanging requests

## Configuration Examples

### Development (appsettings.Development.json)

```json
{
  "ConnectionStrings": {
    "ComplianceDatabase": "Host=localhost;Port=5432;Database=compliance_dev;Username=postgres;Password=dev123"
  },
  "OpaSettings": {
    "BaseUrl": "http://localhost:8181",
    "TimeoutSeconds": 30
  },
  "Logging": {
    "EnableSensitiveDataLogging": true,
    "EnableDetailedErrors": true
  }
}
```

### Production (appsettings.Production.json)

```json
{
  "ConnectionStrings": {
    "ComplianceDatabase": "Host=postgres.example.com;Port=5432;Database=compliance_prod;Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require"
  },
  "OpaSettings": {
    "BaseUrl": "https://opa-sidecar:8181",
    "TimeoutSeconds": 10
  },
  "Logging": {
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": false
  }
}
```

## Deployment

### Database Initialization

1. **Create Database:**
   ```sql
   CREATE DATABASE compliance_service;
   CREATE SCHEMA compliance;
   ```

2. **Run Migrations:**
   ```bash
   dotnet ef database update --project ComplianceService.Infrastructure
   ```

3. **Verify Schema:**
   ```sql
   SELECT table_name FROM information_schema.tables WHERE table_schema = 'compliance';
   ```

### OPA Sidecar Deployment

**Docker Compose:**
```yaml
version: '3.8'
services:
  opa:
    image: openpolicyagent/opa:latest
    ports:
      - "8181:8181"
    command:
      - "run"
      - "--server"
      - "--addr=0.0.0.0:8181"
      - "/policies"
    volumes:
      - ./policies:/policies
```

**Kubernetes Sidecar:**
```yaml
containers:
- name: compliance-service
  image: compliance-service:latest
- name: opa-sidecar
  image: openpolicyagent/opa:latest
  args:
    - "run"
    - "--server"
    - "--addr=127.0.0.1:8181"
```

## Future Enhancements

### Planned Features

1. **Outbox Pattern**: Reliable domain event publishing
2. **Change Data Capture**: Stream audit logs to data warehouse
3. **Read Replicas**: Separate read/write database connections
4. **Materialized Views**: Pre-computed statistics for dashboards
5. **Email/Slack Notifications**: Replace logging-based notifications
6. **Caching Layer**: Redis for configuration caching
7. **Bulk Operations**: Batch inserts for high-volume audit logs

### Performance Optimizations

- **Database Sharding**: Partition data by application or date range
- **CQRS Read Models**: Separate read/write data models
- **Event Sourcing**: Store state changes as events (audit logs already follow this pattern)

## Dependencies

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

## Related Documentation

- [Domain Layer README](../ComplianceService.Domain/README.md) - Domain models and business logic
- [Application Layer README](../ComplianceService.Application/README.md) - Use cases and DTOs
- [OPA Policy Documentation](../../docs/opa-policies.md) - Policy authoring guide (if exists)

## Summary

The Infrastructure layer provides:
- ✅ **PostgreSQL persistence** with EF Core
- ✅ **Repository implementations** for all aggregates
- ✅ **OPA HTTP client** for policy evaluation
- ✅ **Notification service** (logging-based, extensible)
- ✅ **Entity configurations** with value object conversions
- ✅ **Database migrations** for schema management
- ✅ **Dependency injection** setup

This layer is fully decoupled from the Domain and Application layers, following Clean Architecture principles.
