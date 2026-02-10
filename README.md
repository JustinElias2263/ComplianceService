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

### 2. Security Tool Integration
Seamlessly integrate multiple security scanning tools:
- **Snyk** - Dependency vulnerability scanning
- **Prisma Cloud** - Container and cloud security
- Tool registration per application
- Ensure OPA applies correct compliance evaluations based on scan results

### 3. Direct Pipeline Architecture
Streamlined evaluation flow with no normalization overhead:

```
CI Pipeline → ComplianceService → OPA → ComplianceService → CI Pipeline
```

- Direct integration without data transformation layers
- Fast evaluation response times
- Raw scan results processed in real-time
- Policy decisions returned immediately to pipeline

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
- Snyk and/or Prisma Cloud credentials

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd ComplianceService

# Restore dependencies
dotnet restore

# Update database connection string in appsettings.json
# Set up environment variables for security tool credentials

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
  "SecurityTools": {
    "Snyk": {
      "ApiUrl": "https://api.snyk.io",
      "ApiToken": "env:SNYK_TOKEN"
    },
    "PrismaCloud": {
      "ApiUrl": "https://api.prismacloud.io",
      "ApiToken": "env:PRISMA_TOKEN"
    }
  }
}
```

## API Usage

### Evaluate Compliance

```bash
POST /api/compliance/evaluate
Content-Type: application/json

{
  "applicationId": "my-payment-app",
  "environment": "production",
  "scanResults": {
    "tool": "snyk",
    "vulnerabilities": [...]
  }
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

```bash
POST /api/applications/{applicationId}/security-tools
Content-Type: application/json

{
  "tools": ["snyk", "prismacloud"],
  "configuration": {
    "snyk": {
      "projectId": "abc-123"
    }
  }
}
```

## OPA Policy Example

Example Rego policy for critical applications:

```rego
package compliance

default allow = false

allow {
    input.riskTier == "critical"
    input.environment == "production"
    count(critical_vulnerabilities) == 0
    count(high_vulnerabilities) == 0
}

allow {
    input.riskTier == "medium"
    input.environment == "staging"
    count(critical_vulnerabilities) <= 2
    count(high_vulnerabilities) <= 5
}

critical_vulnerabilities[vuln] {
    vuln := input.scanResults.vulnerabilities[_]
    vuln.severity == "critical"
}

high_vulnerabilities[vuln] {
    vuln := input.scanResults.vulnerabilities[_]
    vuln.severity == "high"
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
