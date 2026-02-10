# ComplianceService.Api

## Overview

The **API** layer is the presentation layer for the ComplianceService application. It exposes RESTful HTTP endpoints for application registration, compliance evaluation, and audit log queries. Built with ASP.NET Core 8.0, it follows REST best practices and provides comprehensive API documentation via Swagger/OpenAPI.

This layer is the entry point for CI/CD pipelines and external systems to interact with the Compliance Service.

## Architecture

- **Clean Architecture**: API is the outermost layer, depends only on Application layer
- **RESTful Design**: Resource-based endpoints with proper HTTP verbs
- **CQRS Pattern**: Commands and Queries are separated via MediatR
- **Middleware Pipeline**: Global error handling, logging, and cross-cutting concerns

## Technology Stack

- **.NET 8.0**: Framework
- **ASP.NET Core Web API**: REST API framework
- **MediatR**: CQRS command/query dispatching
- **FluentValidation**: Request validation
- **Serilog**: Structured logging
- **Swashbuckle**: OpenAPI/Swagger documentation
- **Health Checks**: Database and OPA monitoring

## Project Structure

```
ComplianceService.Api/
├── Controllers/
│   ├── ApplicationController.cs      # Application registration & environments
│   ├── ComplianceController.cs       # Compliance evaluations
│   └── AuditController.cs            # Audit log queries & statistics
├── Middleware/
│   ├── GlobalExceptionMiddleware.cs  # Unhandled exception handler
│   └── RequestLoggingMiddleware.cs   # HTTP request/response logging
├── Program.cs                        # Application startup & configuration
├── appsettings.json                  # Production configuration
├── appsettings.Development.json      # Development configuration
└── ComplianceService.Api.csproj      # Project file
```

## API Endpoints

### Application Management

#### POST /api/application
Register a new application for compliance tracking.

**Request:**
```json
{
  "name": "my-service",
  "owner": "team@example.com"
}
```

**Response:** `201 Created`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "my-service",
  "owner": "team@example.com",
  "environments": [],
  "isActive": true,
  "createdAt": "2026-02-10T10:00:00Z"
}
```

#### GET /api/application/{id}
Get application by ID with all environment configurations.

**Response:** `200 OK`

#### GET /api/application/by-name/{name}
Get application by name.

**Response:** `200 OK`

#### GET /api/application
Get all applications with optional filters.

**Query Parameters:**
- `owner` (optional): Filter by owner
- `activeOnly` (optional): Filter active applications only

**Response:** `200 OK`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "my-service",
    "owner": "team@example.com",
    "environments": [...],
    "isActive": true
  }
]
```

#### PATCH /api/application/{id}/owner
Update application owner.

**Request:**
```json
{
  "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "newOwner": "new-team@example.com"
}
```

**Response:** `200 OK`

#### POST /api/application/{id}/deactivate
Deactivate an application.

**Response:** `204 No Content`

### Environment Management

#### POST /api/application/{id}/environments
Add environment configuration to an application.

**Request:**
```json
{
  "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "environmentName": "production",
  "riskTier": "critical",
  "securityTools": ["snyk", "prisma-cloud"],
  "policyReferences": ["compliance.cicd.production"]
}
```

**Response:** `200 OK`

#### PUT /api/application/{id}/environments/{environmentName}
Update environment configuration.

**Request:**
```json
{
  "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "environmentName": "production",
  "riskTier": "critical",
  "securityTools": ["snyk", "prisma-cloud", "sonarqube"],
  "policyReferences": ["compliance.cicd.production"]
}
```

**Response:** `200 OK`

#### POST /api/application/{id}/environments/{environmentName}/deactivate
Deactivate an environment configuration.

**Response:** `200 OK`

### Compliance Evaluation

#### POST /api/compliance/evaluate
Evaluate compliance for an application deployment.

**Request:**
```json
{
  "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "environment": "production",
  "scanResults": [
    {
      "tool": "snyk",
      "criticalCount": 0,
      "highCount": 2,
      "mediumCount": 5,
      "lowCount": 10,
      "vulnerabilities": [
        {
          "id": "SNYK-JS-LODASH-590103",
          "severity": "high",
          "title": "Prototype Pollution",
          "package": "lodash",
          "version": "4.17.19",
          "fixedVersion": "4.17.21"
        }
      ]
    }
  ]
}
```

**Response:** `200 OK`
```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "environment": "production",
  "riskTier": "critical",
  "scanResults": [...],
  "decision": {
    "evaluationId": "eval-2026-02-10-001",
    "allowed": false,
    "violations": [
      {
        "rule": "no_high_vulnerabilities_in_production",
        "message": "Production deployments must have zero high vulnerabilities",
        "severity": "critical"
      }
    ],
    "reason": "Policy violations detected"
  },
  "evaluatedAt": "2026-02-10T10:00:00Z"
}
```

#### GET /api/compliance/{id}
Get evaluation by ID.

**Response:** `200 OK`

#### GET /api/compliance/application/{applicationId}
Get evaluations for an application.

**Query Parameters:**
- `environment` (optional): Filter by environment
- `days` (optional, default: 7): Number of days to look back

**Response:** `200 OK`

#### GET /api/compliance/recent
Get recent evaluations across all applications.

**Query Parameters:**
- `days` (optional, default: 7): Number of days to look back

**Response:** `200 OK`

#### GET /api/compliance/blocked
Get blocked evaluations (denied deployments).

**Query Parameters:**
- `days` (optional): Number of days to look back

**Response:** `200 OK`

### Audit Logs

#### GET /api/audit/{id}
Get audit log by ID.

**Response:** `200 OK`

#### GET /api/audit/evaluation/{evaluationId}
Get audit log by evaluation ID.

**Response:** `200 OK`

#### GET /api/audit/application/{applicationId}
Get audit logs for an application.

**Query Parameters:**
- `environment` (optional): Filter by environment
- `fromDate` (optional): Start date (ISO 8601)
- `toDate` (optional): End date (ISO 8601)
- `pageSize` (optional, default: 50): Page size
- `pageNumber` (optional, default: 1): Page number

**Response:** `200 OK`
```json
[
  {
    "id": "8f7e6d5c-4b3a-2190-8765-4321dcba0987",
    "evaluationId": "eval-2026-02-10-001",
    "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "applicationName": "my-service",
    "environment": "production",
    "riskTier": "critical",
    "allowed": false,
    "reason": "Policy violations detected",
    "violations": ["no_high_vulnerabilities_in_production"],
    "criticalCount": 0,
    "highCount": 2,
    "mediumCount": 5,
    "lowCount": 10,
    "totalVulnerabilityCount": 17,
    "evaluationDurationMs": 245,
    "evaluatedAt": "2026-02-10T10:00:00Z"
  }
]
```

#### GET /api/audit/blocked
Get blocked decisions (denied deployments).

**Query Parameters:**
- `days` (optional): Number of days to look back
- `limit` (optional): Maximum number of results

**Response:** `200 OK`

#### GET /api/audit/critical-vulnerabilities
Get audit logs with critical vulnerabilities.

**Query Parameters:**
- `days` (optional): Number of days to look back

**Response:** `200 OK`

#### GET /api/audit/risk-tier/{riskTier}
Get audit logs by risk tier (critical, high, medium, low).

**Query Parameters:**
- `fromDate` (optional): Start date
- `toDate` (optional): End date

**Response:** `200 OK`

#### GET /api/audit/statistics
Get audit statistics.

**Query Parameters:**
- `fromDate` (optional): Start date
- `toDate` (optional): End date

**Response:** `200 OK`
```json
{
  "totalEvaluations": 150,
  "allowedCount": 120,
  "blockedCount": 30,
  "blockedPercentage": 20.0,
  "totalCriticalVulnerabilities": 5,
  "totalHighVulnerabilities": 45,
  "evaluationsByEnvironment": {
    "production": 50,
    "staging": 60,
    "development": 40
  },
  "evaluationsByRiskTier": {
    "critical": 50,
    "high": 60,
    "medium": 30,
    "low": 10
  }
}
```

### Health Checks

#### GET /health
Check system health status.

**Response:** `200 OK`
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Healthy",
      "description": null,
      "duration": 23.5
    },
    {
      "name": "opa-sidecar",
      "status": "Healthy",
      "description": null,
      "duration": 12.3
    }
  ],
  "totalDuration": 35.8
}
```

## Middleware

### GlobalExceptionMiddleware

Catches all unhandled exceptions and returns standardized error responses using RFC 7807 Problem Details format.

**Features:**
- Logs exceptions with trace IDs
- Returns different detail levels based on environment
- Includes stack traces in development
- Standardized error format for all errors

**Error Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request",
  "status": 500,
  "detail": "An internal server error occurred",
  "instance": "/api/compliance/evaluate",
  "traceId": "0HMVFE9A7US2K:00000001"
}
```

### RequestLoggingMiddleware

Logs all HTTP requests and responses with execution time.

**Logged Information:**
- HTTP method and path
- Status code
- Execution time in milliseconds
- Exception details if request failed

**Log Output:**
```
[10:00:00 INF] HTTP POST /api/compliance/evaluate started
[10:00:00 INF] HTTP POST /api/compliance/evaluate completed with status 200 in 245ms
```

## Configuration

### appsettings.json

**Database Connection:**
```json
{
  "ConnectionStrings": {
    "ComplianceDatabase": "Host=localhost;Port=5432;Database=compliance_service;Username=postgres;Password=CHANGEME"
  }
}
```

**OPA Settings:**
```json
{
  "OpaSettings": {
    "BaseUrl": "http://localhost:8181",
    "TimeoutSeconds": 30
  }
}
```

**Logging Configuration:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
}
```

### Environment Variables

Override configuration values with environment variables:

```bash
# Database
export ConnectionStrings__ComplianceDatabase="Host=db.example.com;..."

# OPA
export OpaSettings__BaseUrl="http://opa-sidecar:8181"
export OpaSettings__TimeoutSeconds="10"

# Logging
export Serilog__MinimumLevel__Default="Debug"
```

## Running the API

### Development

```bash
cd src/ComplianceService.Api
dotnet run
```

Access Swagger UI at: http://localhost:5000

### Production

```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet ComplianceService.Api.dll
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ComplianceService.Api/ComplianceService.Api.csproj", "src/ComplianceService.Api/"]
COPY ["src/ComplianceService.Application/ComplianceService.Application.csproj", "src/ComplianceService.Application/"]
COPY ["src/ComplianceService.Domain/ComplianceService.Domain.csproj", "src/ComplianceService.Domain/"]
COPY ["src/ComplianceService.Infrastructure/ComplianceService.Infrastructure.csproj", "src/ComplianceService.Infrastructure/"]
RUN dotnet restore "src/ComplianceService.Api/ComplianceService.Api.csproj"
COPY . .
WORKDIR "/src/src/ComplianceService.Api"
RUN dotnet build "ComplianceService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ComplianceService.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ComplianceService.Api.dll"]
```

**Build and Run:**
```bash
docker build -t compliance-service:latest .
docker run -p 5000:80 \
  -e ConnectionStrings__ComplianceDatabase="Host=postgres;..." \
  -e OpaSettings__BaseUrl="http://opa-sidecar:8181" \
  compliance-service:latest
```

## Swagger/OpenAPI Documentation

The API includes comprehensive OpenAPI documentation accessible via Swagger UI.

**Development:** http://localhost:5000
**Swagger JSON:** http://localhost:5000/swagger/v1/swagger.json

**Features:**
- Interactive API testing
- Request/response schemas
- Authentication requirements
- Example payloads

## Error Handling

All API errors follow RFC 7807 Problem Details format.

### Validation Errors (400 Bad Request)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Application Registration Failed",
  "status": 400,
  "detail": "Application name cannot be empty"
}
```

### Not Found Errors (404 Not Found)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Application Not Found",
  "status": 404,
  "detail": "Application with ID 3fa85f64-5717-4562-b3fc-2c963f66afa6 not found"
}
```

### Server Errors (500 Internal Server Error)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request",
  "status": 500,
  "detail": "An internal server error occurred",
  "traceId": "0HMVFE9A7US2K:00000001"
}
```

## Authentication & Authorization

**Current Status:** No authentication (v1.0)

**Planned Features:**
- JWT Bearer authentication
- OAuth 2.0 / OpenID Connect
- API key authentication for CI/CD tools
- Role-based access control (RBAC)
- Policy-based authorization

**Example (Future):**
```csharp
[Authorize(Policy = "RequireAdminRole")]
[HttpPost("{id:guid}/deactivate")]
public async Task<IActionResult> DeactivateApplication(Guid id) { ... }
```

## CORS Configuration

CORS is configured to allow all origins in development. Restrict in production:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://ci.example.com", "https://dashboard.example.com")
            .WithMethods("GET", "POST", "PUT", "PATCH")
            .WithHeaders("Content-Type", "Authorization");
    });
});
```

## Performance

### Response Caching

Add response caching for frequently accessed data:

```csharp
[HttpGet]
[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "owner", "activeOnly" })]
public async Task<IActionResult> GetAllApplications(...) { ... }
```

### Compression

Enable response compression for large payloads:

```csharp
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json" });
});
```

## Monitoring & Observability

### Structured Logging with Serilog

All logs include contextual information:
- Request ID
- User ID (when authenticated)
- Application ID
- Environment
- Execution time

### Health Checks

- **Database**: PostgreSQL connection and query health
- **OPA Sidecar**: HTTP connectivity and OPA health endpoint

### Metrics (Future)

Integrate with Prometheus for metrics:
- Request rate
- Response time (p50, p95, p99)
- Error rate
- Database query duration
- OPA evaluation latency

## Testing

### Integration Tests

Test controllers with in-memory database:

```csharp
public class ApplicationControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApplicationControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterApplication_Returns201Created()
    {
        var request = new { name = "test-app", owner = "test@example.com" };
        var response = await _client.PostAsJsonAsync("/api/application", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

### API Testing with Postman/Insomnia

Import OpenAPI spec from `/swagger/v1/swagger.json` for automated testing.

## Deployment

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: compliance-service
spec:
  replicas: 3
  selector:
    matchLabels:
      app: compliance-service
  template:
    metadata:
      labels:
        app: compliance-service
    spec:
      containers:
      - name: compliance-service
        image: compliance-service:latest
        ports:
        - containerPort: 80
        env:
        - name: ConnectionStrings__ComplianceDatabase
          valueFrom:
            secretKeyRef:
              name: compliance-secrets
              key: database-connection
        - name: OpaSettings__BaseUrl
          value: "http://127.0.0.1:8181"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5
      - name: opa-sidecar
        image: openpolicyagent/opa:latest
        args:
          - "run"
          - "--server"
          - "--addr=127.0.0.1:8181"
        ports:
        - containerPort: 8181
---
apiVersion: v1
kind: Service
metadata:
  name: compliance-service
spec:
  selector:
    app: compliance-service
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: LoadBalancer
```

## CI/CD Pipeline Integration

### GitHub Actions Example

```yaml
name: Compliance Check
on: [push]

jobs:
  compliance:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Run Security Scans
        run: |
          snyk test --json > snyk-results.json
          prisma-cloud scan --output prisma-results.json

      - name: Evaluate Compliance
        run: |
          curl -X POST https://compliance.example.com/api/compliance/evaluate \
            -H "Content-Type: application/json" \
            -d @compliance-request.json \
            -o compliance-result.json

      - name: Check Compliance Result
        run: |
          ALLOWED=$(jq -r '.decision.allowed' compliance-result.json)
          if [ "$ALLOWED" != "true" ]; then
            echo "Deployment blocked by compliance policy"
            jq '.decision.violations' compliance-result.json
            exit 1
          fi
```

## Security Considerations

### Input Validation

- All requests validated via FluentValidation
- Model binding validation
- Custom validators for domain rules

### SQL Injection Prevention

- EF Core uses parameterized queries
- No raw SQL execution

### Error Information Disclosure

- Detailed errors only in development
- Production errors are generic
- Sensitive data excluded from logs

### Rate Limiting (Future)

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", options =>
    {
        options.PermitLimit = 100;
        options.Window = TimeSpan.FromMinutes(1);
    });
});
```

## Dependencies

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
<PackageReference Include="MediatR" Version="12.2.0" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Diagnostics.HealthChecks" Version="2.2.0" />
<PackageReference Include="AspNetCore.HealthChecks.Npgsql" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Uris" Version="8.0.1" />
```

## Related Documentation

- [Domain Layer README](../ComplianceService.Domain/README.md)
- [Application Layer README](../ComplianceService.Application/README.md)
- [Infrastructure Layer README](../ComplianceService.Infrastructure/README.md)

## Summary

The API layer provides:
- ✅ **RESTful HTTP endpoints** for all use cases
- ✅ **Swagger/OpenAPI documentation** for interactive testing
- ✅ **Global error handling** with RFC 7807 Problem Details
- ✅ **Request logging** with execution time tracking
- ✅ **Health checks** for database and OPA
- ✅ **Structured logging** with Serilog
- ✅ **CORS support** for cross-origin requests
- ✅ **Comprehensive configuration** for all environments

The API is production-ready and follows ASP.NET Core best practices!
