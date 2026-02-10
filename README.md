# ComplianceService

A policy gateway for CI/CD pipelines that evaluates security scan results against compliance policies before allowing deployments.

## Overview

ComplianceService acts as an intelligent policy enforcement layer in your CI/CD pipeline, providing centralized compliance evaluation and risk-based decision making. By leveraging Open Policy Agent (OPA) and risk-tier-based application profiles, organizations can maintain consistent security standards across diverse applications without hardcoding policies into individual pipelines.

## Problem Statement

Organizations struggle with:

- ❌ **Hardcoded security thresholds** in CI pipelines
- ❌ **Different risk tolerances** for different applications (payment apps vs internal tools)
- ❌ **Developers managing** compliance policies
- ❌ **Slow policy updates** requiring code changes and deployments
- ❌ **No centralized compliance** decision audit trail

## Solution

- ✅ **Centralized policy evaluation** with OPA Rego policies
- ✅ **Risk-tier-based thresholds** via application profiles
- ✅ **Compliance team autonomy** - update policies without developer involvement
- ✅ **30-second policy deployment** via Git push
- ✅ **Complete audit trail** with decision logs and evidence storage
- ✅ **Scalable** - manage 100+ applications with varying security requirements

## Key Features

### 1. Application Profile Management
Manage applications with configurable profiles for different environments and compliance policies:
- Environment-specific security thresholds (dev, staging, production)
- Risk-tier classification (critical, high, medium, low)
- Custom compliance policy mappings
- Application metadata and ownership

### 2. Security Tool Management
Manage security tool configurations and process their scan results:
- **Snyk** - Dependency vulnerability scanning results
- **Prisma Cloud** - Container and cloud security scan results
- Tool registration per application (metadata only)
- CI pipeline executes security tools and forwards JSON outputs
- ComplianceService processes scan results without direct tool integration
- OPA applies correct compliance evaluations based on application's configured tools

### 3. Direct Pipeline Architecture
Streamlined evaluation flow with no normalization overhead:

```
┌─────────────────────────────────────────────────────────────────┐
│ CI Pipeline                                                      │
│  1. Run Snyk/Prisma Cloud scans                                 │
│  2. Collect JSON outputs from security tools                    │
│  3. Send scan results to ComplianceService ───────────┐         │
│                                                        │         │
│  6. Receive pass/fail decision ◄───────────────────┐  │         │
└────────────────────────────────────────────────────┼──┼─────────┘
                                                     │  │
                                                     │  ▼
                               ┌─────────────────────┴──────────────┐
                               │ ComplianceService                   │
                               │  4. Evaluate with OPA ────┐         │
                               │  5. Return decision ◄──────┘         │
                               └─────────────────────────────────────┘
```

**Flow:**
1. CI pipeline runs security scans (Snyk, Prisma Cloud, etc.)
2. CI pipeline sends JSON scan results to ComplianceService
3. ComplianceService forwards to OPA for policy evaluation
4. OPA evaluates against application's compliance policies
5. ComplianceService returns pass/fail decision
6. CI pipeline proceeds or blocks deployment based on decision

**Benefits:**
- No direct security tool integration required
- CI pipeline controls tool execution
- Raw scan results processed in real-time
- Fast evaluation response times
- Policy decisions returned immediately

### 4. Policy as Code
- OPA Rego policies stored in version control
- Policy changes deployed automatically
- Rollback capability for policy issues
- Compliance team owns policy repository

### 5. Audit & Compliance
- Complete decision history
- Evidence storage for compliance reporting
- Traceability of all security decisions
- Support for regulatory compliance requirements

## Tech Stack

- **.NET 8.0** - Modern, high-performance runtime
- **PostgreSQL** - Reliable relational database for application profiles and audit logs
- **.NET API Services** - RESTful API endpoints for pipeline integration
- **DDD Architecture** - Domain-Driven Design for maintainable, scalable code
- **OPA (Open Policy Agent)** - Policy engine for compliance evaluation

## Architecture

The service follows **Domain-Driven Design (DDD)** principles:

```
┌─────────────────┐
│   CI Pipeline   │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────┐
│    ComplianceService API        │
│  ┌──────────────────────────┐   │
│  │   Application Domain     │   │
│  │  - Profile Management    │   │
│  │  - Security Tool Config  │   │
│  └──────────────────────────┘   │
│  ┌──────────────────────────┐   │
│  │   Evaluation Domain      │   │
│  │  - Scan Result Handler   │   │
│  │  - Policy Evaluator      │   │
│  └──────────────────────────┘   │
│  ┌──────────────────────────┐   │
│  │   Audit Domain           │   │
│  │  - Decision Logging      │   │
│  │  - Evidence Storage      │   │
│  └──────────────────────────┘   │
└────────┬────────────────────┬───┘
         │                    │
         ▼                    ▼
    ┌────────┐         ┌──────────┐
    │  OPA   │         │PostgreSQL│
    └────────┘         └──────────┘
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- PostgreSQL 14+
- OPA installed and running
- CI pipeline configured with Snyk and/or Prisma Cloud

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd ComplianceService

# Restore dependencies
dotnet restore

# Update database connection string in appsettings.json
# Configure OPA endpoint

# Run database migrations
dotnet ef database update

# Run the application
dotnet run
```

### Configuration

Configure the following in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=compliancedb;Username=user;Password=pass"
  },
  "OPA": {
    "Endpoint": "http://localhost:8181/v1/data/compliance"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Note:** ComplianceService does not require direct credentials for security tools (Snyk, Prisma Cloud). The CI pipeline executes these tools and forwards their JSON outputs to ComplianceService for evaluation.

## API Usage

### Evaluate Compliance

CI pipeline sends scan results for evaluation:

```bash
POST /api/compliance/evaluate
Content-Type: application/json

{
  "applicationId": "my-payment-app",
  "environment": "production",
  "scanResults": [
    {
      "tool": "snyk",
      "scanDate": "2024-01-15T10:30:00Z",
      "vulnerabilities": [
        {
          "id": "SNYK-JS-AXIOS-1234567",
          "severity": "high",
          "packageName": "axios",
          "version": "0.21.0",
          "title": "Server-Side Request Forgery (SSRF)"
        }
      ]
    },
    {
      "tool": "prismacloud",
      "scanDate": "2024-01-15T10:32:00Z",
      "vulnerabilities": [
        {
          "id": "CVE-2024-1234",
          "severity": "critical",
          "component": "nginx",
          "version": "1.19.0"
        }
      ]
    }
  ]
}
```

**Response:**
```json
{
  "allowed": false,
  "reason": "Critical vulnerabilities found in production environment",
  "details": {
    "criticalCount": 1,
    "highCount": 1,
    "policyViolations": [
      "Production deployments must have 0 critical vulnerabilities"
    ]
  },
  "evaluationId": "eval-12345",
  "timestamp": "2024-01-15T10:33:00Z"
}
```

### Register Application

```bash
POST /api/applications
Content-Type: application/json

{
  "name": "my-payment-app",
  "riskTier": "critical",
  "environments": {
    "production": {
      "maxCriticalVulnerabilities": 0,
      "maxHighVulnerabilities": 0
    },
    "staging": {
      "maxCriticalVulnerabilities": 2,
      "maxHighVulnerabilities": 5
    }
  }
}
```

### Configure Security Tools

Register which security tools are used for an application (metadata only):

```bash
POST /api/applications/{applicationId}/security-tools
Content-Type: application/json

{
  "tools": ["snyk", "prismacloud"],
  "metadata": {
    "snyk": {
      "projectId": "abc-123",
      "description": "Dependency scanning"
    },
    "prismacloud": {
      "projectId": "xyz-789",
      "description": "Container security"
    }
  }
}
```

**Note:** This endpoint only registers tool metadata. The CI pipeline is responsible for executing the security tools and sending their outputs to ComplianceService.

## OPA Policy Example

Example Rego policy evaluating scan results from multiple security tools:

```rego
package compliance

default allow = false

# Critical applications in production must have zero critical/high vulnerabilities
allow {
    input.riskTier == "critical"
    input.environment == "production"
    count(all_critical_vulnerabilities) == 0
    count(all_high_vulnerabilities) == 0
}

# Medium risk applications in staging can have limited vulnerabilities
allow {
    input.riskTier == "medium"
    input.environment == "staging"
    count(all_critical_vulnerabilities) <= 2
    count(all_high_vulnerabilities) <= 5
}

# Low risk applications in dev have more relaxed thresholds
allow {
    input.riskTier == "low"
    input.environment == "dev"
    count(all_critical_vulnerabilities) <= 5
    count(all_high_vulnerabilities) <= 10
}

# Aggregate critical vulnerabilities from all security tools
all_critical_vulnerabilities[vuln] {
    scan := input.scanResults[_]
    vuln := scan.vulnerabilities[_]
    vuln.severity == "critical"
}

# Aggregate high vulnerabilities from all security tools
all_high_vulnerabilities[vuln] {
    scan := input.scanResults[_]
    vuln := scan.vulnerabilities[_]
    vuln.severity == "high"
}

# Helper to get vulnerability counts by tool
vulnerabilities_by_tool[tool_name] = count {
    scan := input.scanResults[_]
    tool_name := scan.tool
    count := count(scan.vulnerabilities)
}
```

**Input Structure:**
The policy receives scan results from the CI pipeline in this format:
```json
{
  "applicationId": "my-payment-app",
  "riskTier": "critical",
  "environment": "production",
  "scanResults": [
    {
      "tool": "snyk",
      "vulnerabilities": [...]
    },
    {
      "tool": "prismacloud",
      "vulnerabilities": [...]
    }
  ]
}
```

## Development

### Project Structure

```
ComplianceService/
├── src/
│   ├── Domain/              # Domain models and logic
│   ├── Application/         # Use cases and orchestration
│   ├── Infrastructure/      # Data access, external services
│   └── API/                 # Controllers and API configuration
├── tests/
│   ├── UnitTests/
│   └── IntegrationTests/
└── policies/                # OPA Rego policies
```

### Running Tests

```bash
dotnet test
```

### Database Migrations

```bash
# Add a new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Deployment

### Policy Updates
Policies can be updated without redeploying the service:

1. Update Rego policies in version control
2. Policies automatically sync to OPA within 30 seconds
3. No service restart required

### Service Deployment
Standard .NET deployment practices apply:

```bash
# Build for production
dotnet publish -c Release

# Deploy to your hosting environment
# (Azure App Service, Kubernetes, etc.)
```

## Support

For issues and questions:
- Create an issue in the repository
- Contact the compliance team
- Review documentation in `/docs`

## License

[Specify your license here]

---

**Built with ❤️ for better security compliance**
