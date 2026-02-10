# ComplianceService

A policy gateway for CI/CD pipelines that evaluates security scan results against compliance policies before allowing deployments.

## Purpose

ComplianceService solves the critical challenge of maintaining consistent security compliance across multiple applications, environments, and teams while enabling compliance teams to operate independently from development workflows.

### Why ComplianceService Exists

**The Traditional Problem:**
In modern software organizations, security compliance checks are typically embedded directly into CI/CD pipelines as hardcoded thresholds. This creates several operational challenges:
- Every application pipeline must implement its own security logic
- Compliance policy changes require updating hundreds of pipeline configurations
- Developers become gatekeepers for compliance decisions
- Different applications need different security tolerances, but pipelines lack flexibility
- No centralized audit trail across all security decisions
- Compliance teams depend on developers to implement policy changes

**The ComplianceService Solution:**
ComplianceService provides a centralized, policy-driven compliance gateway that sits between your CI/CD pipelines and deployment environments. It decouples security policy management from application pipelines, enabling:
- **Compliance Team Autonomy**: Compliance officers update policies via Git without touching any application code or pipelines
- **Consistent Enforcement**: All applications evaluate against the same policy framework, ensuring uniform security standards
- **Environment-Specific Policies**: Production deployments can enforce zero-tolerance policies while development environments remain flexible
- **Application Risk Tiers**: Critical payment applications face stricter requirements than internal tools
- **Centralized Audit Trail**: Every security decision is logged with complete evidence for regulatory compliance
- **Rapid Policy Deployment**: Policy changes propagate to all applications within 30 seconds via OPA sidecar reload

### Who Should Use This

- **Enterprise organizations** with 50+ applications requiring consistent security compliance
- **Regulated industries** (finance, healthcare, government) needing audit trails for security decisions
- **Multi-team environments** where compliance and development teams need operational independence
- **Organizations using Snyk and/or Prisma Cloud** for security scanning
- **Teams practicing DevSecOps** with security integrated into CI/CD pipelines

## Overview

ComplianceService acts as an intelligent policy enforcement layer in your CI/CD pipeline, providing centralized compliance evaluation and risk-based decision making. By leveraging Open Policy Agent (OPA) deployed as a sidecar and risk-tier-based application profiles, organizations can maintain consistent security standards across diverse applications without hardcoding policies into individual pipelines.

**Architecture Philosophy:**
- **Policy as Code**: All compliance rules stored in version-controlled Rego policies
- **Separation of Concerns**: Application profiles contain metadata; OPA policies contain business logic
- **Sidecar Pattern**: OPA runs alongside the service for microsecond-latency policy evaluation
- **No Normalization**: Raw security tool outputs processed directly for maximum speed
- **Audit-First Design**: Every decision logged with complete scan evidence for compliance reporting

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
**Centralized application configuration with environment-specific compliance requirements**

ComplianceService maintains a registry of all applications with their compliance requirements organized by environment. This enables different security postures for different deployment targets without pipeline modifications.

**Capabilities:**
- **Multi-Environment Support**: Each application defines separate configurations for dev, staging, and production environments
- **Risk-Tier Classification**: Applications categorized as critical, high, medium, or low risk to apply appropriate policy stringency
  - **Critical**: Payment processing, customer PII, financial systems (zero tolerance for vulnerabilities)
  - **High**: Customer-facing applications, authentication services (minimal tolerance)
  - **Medium**: Internal business applications (moderate tolerance)
  - **Low**: Development tools, internal utilities (relaxed policies)
- **Tool Assignment**: Configure which security tools (Snyk, Prisma Cloud) apply to each environment
- **Policy Mapping**: Reference specific OPA policy packages per environment (e.g., `compliance/critical-production`)
- **Ownership Tracking**: Associate applications with teams for notification and accountability
- **Metadata Storage**: Store tool-specific identifiers (project IDs, API keys references) without exposing credentials

**Key Design Decision:**
Application profiles contain **zero business logic**. All security thresholds, vulnerability tolerances, and compliance rules live exclusively in OPA policies. This architectural separation enables compliance teams to update security requirements by modifying Rego policies in Git, while developers manage application metadata through APIs.

**Example Use Case:**
Your payment processing application requires zero critical vulnerabilities in production but allows up to 5 in staging for rapid testing. Rather than hardcoding these thresholds in the application profile, you reference two different policies: `compliance/critical-production` (0 critical vulns) and `compliance/critical-staging` (5 critical vulns). The compliance team can adjust these thresholds anytime by updating the Rego files.

### 2. Security Tool Management
**Process scan results from multiple security vendors without direct integration**

ComplianceService accepts security scan results from any tool that produces JSON output. Your CI pipeline executes security scanners and forwards their outputs to ComplianceService for policy evaluation. This design eliminates the need for direct API integration with security vendors.

**Supported Security Tools:**
- **Snyk**: Dependency vulnerability scanning for open-source libraries
  - Scans package.json, requirements.txt, pom.xml, go.mod, etc.
  - Identifies CVEs in application dependencies
  - Provides severity ratings (critical, high, medium, low)
- **Prisma Cloud** (formerly Twistlock): Container and cloud security
  - Container image vulnerability scanning
  - Compliance checks for Dockerfile configurations
  - Runtime protection policies
  - Cloud infrastructure misconfigurations

**How It Works:**
1. **CI Pipeline Executes Tools**: Your pipeline runs `snyk test --json` or Prisma Cloud CLI commands
2. **JSON Output Collection**: Pipeline captures the raw JSON output from each tool
3. **Forward to ComplianceService**: Pipeline POSTs scan results to `/api/compliance/evaluate`
4. **Tool-Agnostic Processing**: ComplianceService aggregates vulnerabilities across all tools
5. **OPA Evaluation**: Policies count total vulnerabilities regardless of source tool

**Benefits of This Approach:**
- **No Security Tool Credentials in ComplianceService**: CI pipeline manages tool authentication
- **Tool Flexibility**: Easily add new security tools by updating pipeline and OPA policies
- **No API Rate Limits**: ComplianceService doesn't make API calls to security vendors
- **Offline Operation**: Evaluate scans without external API dependencies
- **Version Agnostic**: Works with any version of Snyk/Prisma that outputs JSON

**Multi-Tool Aggregation:**
When multiple security tools scan the same application, ComplianceService aggregates their findings. If Snyk reports 2 critical vulnerabilities and Prisma Cloud reports 3 critical vulnerabilities, OPA policies see 5 total critical vulnerabilities. This prevents the problem of "passing" individual tools but failing overall security posture.

### 3. Direct Pipeline Architecture
**Streamlined evaluation flow with no normalization overhead**

The architecture follows a straight-through processing model where security scan results flow directly from CI pipeline to ComplianceService to OPA without transformation or normalization layers. This design prioritizes performance and simplicity.

```
┌─────────────────────────────────────────────────────────────────────┐
│                          CI/CD Pipeline                              │
│                                                                      │
│  ┌──────────────┐       ┌────────────────────┐                     │
│  │ Run Snyk     │       │ Run Prisma Cloud   │                     │
│  │ Scan         │       │ Scan               │                     │
│  └──────┬───────┘       └─────────┬──────────┘                     │
│         │                          │                                 │
│         └──────────┬───────────────┘                                │
│                    ▼                                                 │
│         ┌─────────────────────┐                                     │
│         │ Collect JSON        │                                     │
│         │ Outputs from Tools  │                                     │
│         └──────────┬──────────┘                                     │
│                    │                                                 │
│                    │ POST /api/compliance/evaluate                  │
│                    │ {app, env, scanResults[]}                      │
└────────────────────┼─────────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────────────┐
│              ComplianceService Pod/Container                         │
│  ┌────────────────────────────────────────────────────────────┐    │
│  │  ComplianceService API (.NET 8.0)                          │    │
│  │  ┌─────────────────────────────────────────────────────┐   │    │
│  │  │ 1. Retrieve Application Profile from PostgreSQL     │   │    │
│  │  │ 2. Get Environment Config (tools, policies)         │   │    │
│  │  │ 3. Build OPA input payload                          │   │    │
│  │  └─────────────────────────────────────────────────────┘   │    │
│  └────────────────────┬───────────────────────────────────────┘    │
│                       │                                             │
│                       │ Local HTTP Query                            │
│                       │ http://localhost:8181/v1/data/...           │
│                       ▼                                             │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  OPA Sidecar Container                                      │   │
│  │  ┌───────────────────────────────────────────────────────┐  │   │
│  │  │ 4. Count vulnerabilities by severity                  │  │   │
│  │  │ 5. Apply environment-specific thresholds              │  │   │
│  │  │ 6. Check risk-tier requirements                       │  │   │
│  │  │ 7. Return decision {allow, violations, details}       │  │   │
│  │  └───────────────────────────────────────────────────────┘  │   │
│  └─────────────────────┬───────────────────────────────────────┘   │
│                        │                                            │
│  ┌─────────────────────▼───────────────────────────────────────┐   │
│  │  ComplianceService API                                      │   │
│  │  ┌───────────────────────────────────────────────────────┐  │   │
│  │  │ 8. Log decision to PostgreSQL audit table             │  │   │
│  │  │ 9. Store scan evidence                                │  │   │
│  │  │ 10. Return response to CI pipeline                    │  │   │
│  │  └───────────────────────────────────────────────────────┘  │   │
│  └──────────────────────┬──────────────────────────────────────┘   │
└─────────────────────────┼──────────────────────────────────────────┘
                          │
                          │ Response: {allowed, reason, evaluationId}
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          CI/CD Pipeline                              │
│                    ┌─────────────────┐                              │
│                    │   Decision?     │                              │
│                    └────┬──────┬─────┘                              │
│                         │      │                                     │
│                 allowed=true   allowed=false                        │
│                         │      │                                     │
│                         ▼      ▼                                     │
│              ┌─────────────┐  ┌─────────────────┐                  │
│              │   Deploy    │  │ Block Deploy &  │                  │
│              │ Application │  │  Notify Team    │                  │
│              └─────────────┘  └─────────────────┘                  │
└─────────────────────────────────────────────────────────────────────┘

          ┌─────────────────────────────────────────────┐
          │     PostgreSQL Database                      │
          │  ┌────────────────────────────────────────┐ │
          │  │ - Application Profiles                 │ │
          │  │ - Environment Configurations           │ │
          │  │ - Audit Logs (all decisions)           │ │
          │  │ - Scan Evidence (JSON storage)         │ │
          │  └────────────────────────────────────────┘ │
          └─────────────────────────────────────────────┘
```

**Diagram Explanation:**

This diagram illustrates the end-to-end flow of a security compliance evaluation:

**Phase 1: Security Scanning (CI Pipeline)**
- CI pipeline executes security scanners (Snyk, Prisma Cloud) as separate jobs
- Each tool produces JSON output with vulnerability details
- Pipeline collects all JSON outputs into a single array
- No preprocessing or transformation occurs at this stage

**Phase 2: Compliance Evaluation (ComplianceService Pod)**
- Request arrives at ComplianceService API with application ID, environment, and scan results
- API queries PostgreSQL for application profile (risk tier, owner)
- API retrieves environment configuration (which policies apply, which tools expected)
- API constructs OPA input payload with all relevant data
- API makes local HTTP call to OPA sidecar (same pod, localhost:8181)
- OPA evaluates Rego policy and returns decision object
- API logs complete decision to PostgreSQL audit table
- API stores original scan results as evidence
- API returns pass/fail decision to CI pipeline

**Phase 3: Deployment Decision (CI Pipeline)**
- Pipeline receives decision response
- If allowed=true: Proceed with deployment (kubectl apply, helm upgrade, etc.)
- If allowed=false: Block deployment, fail pipeline, notify team via Slack/email

**Key Architectural Decisions:**

1. **Sidecar Pattern**: OPA runs in same pod as ComplianceService for microsecond-latency queries
   - No network hops for policy evaluation
   - Both containers scale together
   - Simplified deployment model

2. **No Normalization**: Raw scan outputs sent directly to OPA
   - Eliminates transformation layer complexity
   - Reduces latency (no parsing/mapping/reformatting)
   - Policies work with native tool formats

3. **Stateless API**: ComplianceService doesn't cache decisions
   - Every evaluation is independent
   - Enables horizontal scaling
   - All state persisted to PostgreSQL

4. **Audit-First**: Every decision logged before response
   - Compliance reporting requirements satisfied
   - Complete evidence trail for security audits
   - Enables post-incident investigation

**Performance Characteristics:**
- **Typical Response Time**: 50-150ms (including database queries and OPA evaluation)
- **OPA Evaluation**: <5ms (local sidecar query)
- **Database Operations**: 20-50ms (profile lookup + audit insert)
- **Throughput**: 500+ evaluations/second per pod (horizontally scalable)

**Benefits:**
- ✅ **No Security Tool Integration**: CI pipeline owns tool execution and credentials
- ✅ **Sub-Second Policy Evaluation**: OPA sidecar provides microsecond-latency decisions
- ✅ **No Normalization Overhead**: Raw scan results processed directly
- ✅ **Complete Audit Trail**: Every decision logged with evidence for compliance
- ✅ **Horizontal Scalability**: Stateless design enables unlimited scaling

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

### Core Technologies

#### .NET 8.0 Runtime
**Modern, high-performance cross-platform runtime**

- **SDK**: .NET 8.0.x LTS (Long-Term Support until November 2026)
- **C# Version**: C# 12 with nullable reference types enabled
- **Runtime Features**:
  - Native AOT compilation support for reduced startup time
  - Performance improvements (20-30% faster than .NET 7)
  - Improved JSON serialization performance
  - Enhanced diagnostics and observability

**Key NuGet Packages:**

```xml
<!-- Web Framework -->
<PackageReference Include="Microsoft.AspNetCore.App" Version="8.0.*" />
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.*" /> <!-- API documentation -->

<!-- Data Access - Entity Framework Core -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.*" />

<!-- OPA Integration -->
<PackageReference Include="RestSharp" Version="110.*" /> <!-- HTTP client for OPA queries -->
<PackageReference Include="Polly" Version="8.2.*" /> <!-- Resilience and retry policies -->

<!-- JSON Processing -->
<PackageReference Include="System.Text.Json" Version="8.0.*" /> <!-- High-performance JSON -->
<PackageReference Include="Newtonsoft.Json" Version="13.0.*" /> <!-- Legacy compatibility -->

<!-- Validation -->
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.*" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.3.*" />

<!-- Logging & Observability -->
<PackageReference Include="Serilog.AspNetCore" Version="8.0.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.*" />
<PackageReference Include="Serilog.Sinks.PostgreSQL" Version="2.3.*" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.6.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.6.*" />

<!-- Health Checks -->
<PackageReference Include="AspNetCore.HealthChecks.Npgsql" Version="7.1.*" />
<PackageReference Include="AspNetCore.HealthChecks.Uris" Version="7.0.*" /> <!-- OPA health check -->

<!-- Domain-Driven Design -->
<PackageReference Include="MediatR" Version="12.2.*" /> <!-- CQRS pattern -->
<PackageReference Include="MediatR.Extensions.Microsoft.DependencyInjection" Version="11.1.*" />

<!-- Testing -->
<PackageReference Include="xunit" Version="2.6.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.*" />
<PackageReference Include="Moq" Version="4.20.*" />
<PackageReference Include="FluentAssertions" Version="6.12.*" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.6.*" /> <!-- Integration tests -->
```

#### PostgreSQL 14+
**Enterprise-grade relational database**

- **Version**: PostgreSQL 14.x or 15.x (recommended 15.x for JSON performance)
- **Driver**: Npgsql 8.0 - High-performance .NET data provider
- **Features Used**:
  - JSONB columns for storing scan results (indexed for queries)
  - Partitioning for audit tables (partition by month for performance)
  - Row-level security for multi-tenant applications
  - Full-text search for vulnerability descriptions

**Database Schema:**
```sql
-- Application profiles
CREATE TABLE application_profiles (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    risk_tier VARCHAR(50) NOT NULL, -- critical, high, medium, low
    owner VARCHAR(255),
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

-- Environment configurations
CREATE TABLE environment_configs (
    id UUID PRIMARY KEY,
    application_id UUID REFERENCES application_profiles(id),
    environment_name VARCHAR(100) NOT NULL, -- production, staging, dev
    security_tools JSONB, -- ["snyk", "prismacloud"]
    policies JSONB, -- ["compliance/critical-production"]
    metadata JSONB, -- Tool-specific config
    UNIQUE(application_id, environment_name)
);

-- Audit logs (partitioned by month)
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY,
    evaluation_id VARCHAR(255) UNIQUE,
    application_id UUID REFERENCES application_profiles(id),
    environment VARCHAR(100),
    risk_tier VARCHAR(50),
    allowed BOOLEAN,
    reason TEXT,
    violations JSONB,
    scan_evidence JSONB, -- Complete scan results
    evaluation_duration_ms INTEGER,
    created_at TIMESTAMP DEFAULT NOW()
) PARTITION BY RANGE (created_at);
```

**Performance Optimizations:**
- Indexes on application_id, environment, created_at for fast audit queries
- JSONB GIN indexes for searching scan evidence
- Connection pooling (min: 10, max: 100 connections)
- Statement timeout: 30 seconds

#### OPA (Open Policy Agent) 0.60+
**Policy engine for policy-as-code enforcement**

- **Version**: OPA 0.60.0+ (latest stable)
- **Deployment**: Sidecar container in same pod as ComplianceService
- **Image**: `openpolicyagent/opa:0.60.0-rootless`
- **Policy Language**: Rego (declarative policy language)

**OPA Configuration:**
```yaml
# OPA config.yaml
services:
  bundle-server:
    url: https://git-repo.com/compliance-policies
    credentials:
      bearer:
        token: ${GIT_TOKEN}

bundles:
  complianceservice:
    service: bundle-server
    resource: /bundles/compliance.tar.gz
    polling:
      min_delay_seconds: 10
      max_delay_seconds: 30

decision_logs:
  console: true

plugins:
  envoy_ext_authz_grpc:
    addr: ":9191"
    query: data.compliance.evaluate
```

**OPA Features Used:**
- **Bundle Management**: Policies loaded from Git repository
- **Auto-reload**: Policies refresh every 30 seconds from Git
- **Decision Logging**: All policy evaluations logged
- **Built-in Functions**: String manipulation, regex, crypto functions
- **Performance**: <5ms typical evaluation time

**Rego Policy Dependencies:**
- `future.keywords.if` - Modern Rego syntax
- `array.concat` - Array manipulation
- `count()` - Aggregation functions
- `sprintf()` - String formatting for violation messages

### Architecture Patterns

#### Domain-Driven Design (DDD)
**Layered architecture with clear domain boundaries**

**Bounded Contexts:**
1. **Application Domain**: Manages application profiles and environment configs
2. **Evaluation Domain**: Orchestrates compliance evaluations
3. **Audit Domain**: Handles decision logging and evidence storage

**DDD Patterns Implemented:**
- **Aggregates**: ApplicationProfile (aggregate root), EnvironmentConfig (entity)
- **Value Objects**: RiskTier, PolicyReference, ScanResult
- **Domain Events**: ApplicationRegistered, ComplianceEvaluated, PolicyViolationDetected
- **Repositories**: ApplicationRepository, AuditRepository
- **Domain Services**: ComplianceEvaluationService, OPAClientService

**Project Structure:**
```
src/
├── Domain/
│   ├── ApplicationProfile/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Events/
│   │   └── Interfaces/
│   ├── Evaluation/
│   └── Audit/
├── Application/
│   ├── Commands/ (CQRS command handlers)
│   ├── Queries/ (CQRS query handlers)
│   └── DTOs/
├── Infrastructure/
│   ├── Persistence/ (EF Core DbContext, repositories)
│   ├── OPA/ (OPA HTTP client)
│   └── Configuration/
└── API/
    ├── Controllers/
    ├── Middleware/
    └── Extensions/
```

#### CQRS Pattern
**Separate read and write operations for scalability**

- **Commands**: RegisterApplication, EvaluateCompliance
- **Queries**: GetApplicationProfile, GetAuditLogs
- **Mediator**: MediatR library for command/query routing
- **Benefits**: Optimized queries, separate scaling, event sourcing ready

### Development Tools

- **IDE**: Visual Studio 2022 or JetBrains Rider
- **CLI**: dotnet CLI 8.0
- **Database Migrations**: Entity Framework Core Migrations
- **API Testing**: Swagger UI, Postman collections
- **Container Runtime**: Docker 24+ or Podman
- **Orchestration**: Kubernetes 1.28+ (for production)

### Security & Compliance

- **Authentication**: JWT Bearer tokens (integrate with your IdP)
- **Authorization**: Role-based access control (RBAC)
- **Secrets Management**: Kubernetes Secrets or Azure Key Vault
- **TLS**: Certificate management via cert-manager (Kubernetes)
- **Audit Logging**: Structured JSON logs to PostgreSQL and stdout

## Architecture

**Domain-Driven Design with Sidecar Pattern**

The service follows **Domain-Driven Design (DDD)** principles with OPA deployed as a sidecar container for low-latency policy evaluation. This architecture separates business domains while maintaining deployment simplicity through the sidecar pattern.

```
┌──────────────────────────────────────────────────────────────────────┐
│                         CI/CD Pipeline                                │
│                                                                       │
│  Jenkins / GitHub Actions / GitLab CI / Azure DevOps                 │
│  - Executes Snyk, Prisma Cloud scans                                 │
│  - Sends results to ComplianceService                                │
│  - Receives pass/fail decision                                       │
└────────────────────────┬─────────────────────────────────────────────┘
                         │
                         │ HTTP/HTTPS
                         │ POST /api/compliance/evaluate
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                   Kubernetes Pod: ComplianceService                     │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │         Container 1: ComplianceService API (.NET 8.0)             │ │
│  │                                                                   │ │
│  │  ┌─────────────────────────────────────────────────────────────┐ │ │
│  │  │ Application Domain                                          │ │ │
│  │  │ ─────────────────────────────────────────────────────────── │ │ │
│  │  │ • ApplicationProfile (Aggregate Root)                       │ │ │
│  │  │ • EnvironmentConfig (Entity)                                │ │ │
│  │  │ • RegisterApplicationCommand                                │ │ │
│  │  │ • ApplicationRepository                                      │ │ │
│  │  └─────────────────────────────────────────────────────────────┘ │ │
│  │                                                                   │ │
│  │  ┌─────────────────────────────────────────────────────────────┐ │ │
│  │  │ Evaluation Domain                                           │ │ │
│  │  │ ─────────────────────────────────────────────────────────── │ │ │
│  │  │ • ComplianceEvaluation (Aggregate Root)                     │ │ │
│  │  │ • ScanResult (Value Object)                                 │ │ │
│  │  │ • EvaluateComplianceCommand                                 │ │ │
│  │  │ • OPAClientService (Domain Service)                         │ │ │
│  │  └─────────────────────────────────────────────────────────────┘ │ │
│  │                                                                   │ │
│  │  ┌─────────────────────────────────────────────────────────────┐ │ │
│  │  │ Audit Domain                                                │ │ │
│  │  │ ─────────────────────────────────────────────────────────── │ │ │
│  │  │ • AuditLog (Aggregate Root)                                 │ │ │
│  │  │ • DecisionEvidence (Value Object)                           │ │ │
│  │  │ • LogComplianceDecisionCommand                              │ │ │
│  │  │ • AuditRepository                                            │ │ │
│  │  └─────────────────────────────────────────────────────────────┘ │ │
│  │                                                                   │ │
│  │  Port 5000 ─────────────────────────────────────────────────────│ │
│  └───────────────────────────┬───────────────────────────────────────┘ │
│                              │                                          │
│                              │ HTTP localhost:8181                      │
│                              │ /v1/data/compliance/evaluate             │
│                              ▼                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │         Container 2: OPA Sidecar (openpolicyagent/opa)         │   │
│  │                                                                 │   │
│  │  • Policy Engine (Rego evaluation)                             │   │
│  │  • Bundle Management (Git sync)                                │   │
│  │  • Decision Caching                                            │   │
│  │  • Metrics & Logging                                           │   │
│  │                                                                 │   │
│  │  Port 8181 ───────────────────────────────────────────────────│   │
│  └─────────────────────────┬───────────────────────────────────────┘   │
│                            │                                            │
│                            │ Polls every 30s                            │
└────────────────────────────┼────────────────────────────────────────────┘
                             │
                             ▼
                  ┌─────────────────────────────┐
                  │   Git Repository            │
                  │   (Policy Bundles)          │
                  │                             │
                  │  compliance/                │
                  │  ├── critical-production    │
                  │  ├── critical-staging       │
                  │  ├── dev-relaxed            │
                  │  └── common/                │
                  └─────────────────────────────┘

         ┌──────────────────────────────────────────────────┐
         │  PostgreSQL Database                             │
         │  ┌────────────────────────────────────────────┐  │
         │  │ application_profiles                       │  │
         │  │ - id, name, risk_tier, owner               │  │
         │  ├────────────────────────────────────────────┤  │
         │  │ environment_configs                        │  │
         │  │ - id, app_id, env_name, tools, policies   │  │
         │  ├────────────────────────────────────────────┤  │
         │  │ audit_logs (partitioned by month)          │  │
         │  │ - id, evaluation_id, app_id, allowed       │  │
         │  │ - violations, scan_evidence (JSONB)        │  │
         │  └────────────────────────────────────────────┘  │
         └──────────────────────────────────────────────────┘
                   ▲
                   │ Npgsql Driver
                   │ (EF Core)
                   │
         ┌─────────┴──────────────────────────────┐
         │  ComplianceService API                 │
         │  (Infrastructure Layer)                │
         └────────────────────────────────────────┘
```

**Diagram Explanation:**

This architecture diagram illustrates the complete system topology showing how all components interact:

**Deployment Unit (Kubernetes Pod):**
The ComplianceService and OPA Sidecar deploy together as a single Kubernetes pod with two containers:
- **Container 1**: ComplianceService API (.NET 8.0) - Handles HTTP requests, domain logic, database access
- **Container 2**: OPA Sidecar - Evaluates Rego policies, manages policy bundles

**Domain-Driven Design Structure:**
The API container is organized into three bounded contexts following DDD principles:

1. **Application Domain**:
   - **Purpose**: Manage application registrations and environment configurations
   - **Key Entities**: ApplicationProfile (aggregate root), EnvironmentConfig
   - **Commands**: RegisterApplication, UpdateEnvironmentConfig
   - **Queries**: GetApplicationProfile, ListApplications

2. **Evaluation Domain**:
   - **Purpose**: Orchestrate compliance evaluations by coordinating with OPA
   - **Key Entities**: ComplianceEvaluation (aggregate root)
   - **Services**: OPAClientService (communicates with sidecar)
   - **Commands**: EvaluateCompliance
   - **Value Objects**: ScanResult, PolicyDecision

3. **Audit Domain**:
   - **Purpose**: Maintain complete audit trail of all compliance decisions
   - **Key Entities**: AuditLog (aggregate root)
   - **Commands**: LogComplianceDecision
   - **Queries**: GetAuditHistory, SearchViolations

**Sidecar Communication:**
- ComplianceService queries OPA via HTTP on `localhost:8181`
- No network latency (same pod, loopback interface)
- Typical query time: <5ms
- Connection pooling with keep-alive for performance

**Policy Management:**
- OPA polls Git repository every 30 seconds for policy updates
- Policies loaded as bundles (tar.gz of Rego files)
- Hot reload without pod restart
- Version control for policy changes

**Data Persistence:**
- PostgreSQL stores all application metadata and audit logs
- JSONB columns for flexible scan evidence storage
- Partitioned audit tables for query performance
- Entity Framework Core for ORM

**Key Architectural Benefits:**

1. **Low Latency**: Sidecar pattern eliminates network hops (localhost:8181)
2. **Deployment Simplicity**: Single pod to deploy, scale, and manage
3. **Policy Independence**: Compliance team updates policies without touching service
4. **Domain Isolation**: Each bounded context has clear responsibilities
5. **Horizontal Scalability**: Stateless design enables unlimited pod replicas
6. **Audit Completeness**: Every decision logged with full evidence

**Key Components:**

- **ComplianceService API**: .NET 8.0 service with three DDD bounded contexts (Application, Evaluation, Audit)
- **OPA Sidecar**: Policy engine running in same pod for microsecond-latency evaluation
- **PostgreSQL 14+**: Relational database with JSONB support for scan evidence storage
- **Policy Repository**: Git-based version control for Rego policies with automated synchronization

## API Flow - Compliance Evaluation

**Detailed sequence showing request/response flow through all system components**

This diagram illustrates the complete lifecycle of a compliance evaluation from CI pipeline request to deployment decision.

```
CI Pipeline          ComplianceService API       PostgreSQL DB        OPA Sidecar
    │                         │                        │                    │
    │ 1. Run Snyk scan       │                        │                    │
    │ 2. Run Prisma scan     │                        │                    │
    │ 3. Collect JSON        │                        │                    │
    │                         │                        │                    │
    │─────────────────────────────────────────────────────────────────────────
    │                         │                        │                    │
    │  POST /api/compliance/evaluate                   │                    │
    │  {                      │                        │                    │
    │    applicationId: "payment-app",                 │                    │
    │    environment: "production",                    │                    │
    │    scanResults: [...]   │                        │                    │
    │  }                      │                        │                    │
    │────────────────────────>│                        │                    │
    │                         │                        │                    │
    │                         │  [Evaluation Domain]   │                    │
    │                         │                        │                    │
    │                         │  SELECT * FROM         │                    │
    │                         │  application_profiles  │                    │
    │                         │  WHERE id = ?          │                    │
    │                         │───────────────────────>│                    │
    │                         │                        │                    │
    │                         │  {id, name, riskTier:  │                    │
    │                         │   "critical", owner}   │                    │
    │                         │<───────────────────────│                    │
    │                         │                        │                    │
    │                         │  SELECT * FROM         │                    │
    │                         │  environment_configs   │                    │
    │                         │  WHERE app_id = ? AND  │                    │
    │                         │  environment = ?       │                    │
    │                         │───────────────────────>│                    │
    │                         │                        │                    │
    │                         │  {tools: ["snyk",      │                    │
    │                         │   "prismacloud"],      │                    │
    │                         │   policies: ["comp..."]│                    │
    │                         │<───────────────────────│                    │
    │                         │                        │                    │
    │                         │  [Build OPA Input]     │                    │
    │                         │  Construct policy      │                    │
    │                         │  input payload         │                    │
    │                         │                        │                    │
    │                         │  POST /v1/data/compliance/evaluate          │
    │                         │  {                     │                    │
    │                         │    input: {            │                    │
    │                         │      applicationId,    │                    │
    │                         │      riskTier,         │                    │
    │                         │      environment,      │                    │
    │                         │      policies,         │                    │
    │                         │      scanResults       │                    │
    │                         │    }                   │                    │
    │                         │  }                     │                    │
    │                         │────────────────────────────────────────────>│
    │                         │                        │                    │
    │                         │                        │  [Evaluate Rego]   │
    │                         │                        │  1. Load policies  │
    │                         │                        │  2. Count vulns    │
    │                         │                        │  3. Check thresh   │
    │                         │                        │  4. Build decision │
    │                         │                        │                    │
    │                         │  {                     │                    │
    │                         │    result: {           │                    │
    │                         │      allow: false,     │                    │
    │                         │      violations: [     │                    │
    │                         │        "Critical..."], │                    │
    │                         │      details: {...}    │                    │
    │                         │    }                   │                    │
    │                         │  }                     │                    │
    │                         │<────────────────────────────────────────────│
    │                         │                        │                    │
    │                         │  [Audit Domain]        │                    │
    │                         │                        │                    │
    │                         │  INSERT INTO audit_logs│                    │
    │                         │  (evaluation_id,       │                    │
    │                         │   application_id,      │                    │
    │                         │   allowed, violations, │                    │
    │                         │   scan_evidence, ...)  │                    │
    │                         │───────────────────────>│                    │
    │                         │                        │                    │
    │                         │  {audit_id: UUID}      │                    │
    │                         │<───────────────────────│                    │
    │                         │                        │                    │
    │  {                      │                        │                    │
    │    allowed: false,      │                        │                    │
    │    reason: "Critical...",                        │                    │
    │    details: {           │                        │                    │
    │      criticalCount: 1,  │                        │                    │
    │      highCount: 2,      │                        │                    │
    │      policyViolations:  │                        │                    │
    │        [...]            │                        │                    │
    │    },                   │                        │                    │
    │    evaluationId: "eval-123"                      │                    │
    │  }                      │                        │                    │
    │<────────────────────────│                        │                    │
    │                         │                        │                    │
    │  [Decision Logic]       │                        │                    │
    │                         │                        │                    │
    │  if allowed == true:    │                        │                    │
    │    ┌─────────────────┐  │                        │                    │
    │    │ Deploy to prod  │  │                        │                    │
    │    │ kubectl apply   │  │                        │                    │
    │    └─────────────────┘  │                        │                    │
    │  else:                  │                        │                    │
    │    ┌─────────────────┐  │                        │                    │
    │    │ Fail pipeline   │  │                        │                    │
    │    │ Send Slack msg  │  │                        │                    │
    │    │ Block deployment│  │                        │                    │
    │    └─────────────────┘  │                        │                    │
    │                         │                        │                    │
```

**Sequence Explanation:**

**Steps 1-3: Security Scanning (CI Pipeline)**
- CI pipeline executes security scanners as part of build process
- Snyk scans dependencies, Prisma Cloud scans containers
- JSON outputs collected into array for submission

**Step 4: HTTP Request to ComplianceService**
- CI makes POST request to `/api/compliance/evaluate`
- Payload contains application ID, environment name, and scan results array
- Request routed to Evaluation Domain controller

**Steps 5-6: Application Profile Retrieval**
- Query PostgreSQL for application profile by ID
- Retrieve risk tier (critical/high/medium/low) and ownership info
- Risk tier determines which policy stringency applies

**Steps 7-8: Environment Configuration Lookup**
- Query for environment-specific configuration
- Retrieve which security tools are configured (snyk, prismacloud)
- Get list of OPA policy packages to evaluate

**Step 9: OPA Input Construction**
- Evaluation Domain assembles complete input for OPA
- Combines profile data, environment config, and scan results
- Formats as JSON payload for OPA query

**Steps 10-11: OPA Policy Evaluation**
- HTTP POST to OPA sidecar on localhost:8181
- OPA loads referenced policy packages
- Rego policies count vulnerabilities by severity across all tools
- Policies apply environment-specific thresholds
- OPA returns decision object with allow boolean and violation details

**Steps 12-13: Audit Logging**
- Audit Domain logs complete decision to PostgreSQL
- Stores evaluation ID, decision, violations, original scan results
- JSONB column preserves complete evidence for compliance audits
- Returns audit ID confirming persistence

**Step 14: Response to CI Pipeline**
- API returns decision response with allowed flag
- Includes human-readable reason and detailed violation breakdown
- Provides evaluation ID for traceability

**Step 15: Deployment Decision**
- CI pipeline checks `allowed` field
- If true: Proceeds with deployment (kubectl, helm, etc.)
- If false: Fails pipeline, notifies team, blocks deployment

**Timing Breakdown:**
- Database queries: ~20-30ms (connection pooled)
- OPA evaluation: ~3-8ms (localhost sidecar)
- Audit logging: ~15-25ms (async possible)
- **Total typical response time: 50-100ms**

**Key Architectural Points:**
- ✅ **Application profiles determine policies** - Not hardcoded thresholds
- ✅ **Environment-specific evaluation** - Production vs staging policies
- ✅ **OPA contains all threshold logic** - Policies define business rules
- ✅ **Complete audit trail** - Every decision logged with evidence
- ✅ **Fast response time** - Sub-100ms typical latency

## OPA Integration - Input/Output Formatting

**Detailed guide on formatting data for OPA policy evaluation**

ComplianceService communicates with OPA via HTTP JSON API. Understanding the exact input structure and output format is critical for writing effective Rego policies and processing decisions correctly.

### OPA Input Structure

When ComplianceService queries OPA, it sends a POST request to `http://localhost:8181/v1/data/compliance/evaluate` with the following structure:

```json
{
  "input": {
    "applicationId": "payment-processing-api",
    "applicationName": "Payment Processing API",
    "riskTier": "critical",
    "environment": "production",
    "owner": "payments-team@company.com",
    "policies": [
      "compliance/critical_production",
      "compliance/zero_critical_vulns"
    ],
    "scanResults": [
      {
        "tool": "snyk",
        "toolVersion": "1.1200.0",
        "scanDate": "2024-01-15T10:30:00Z",
        "projectId": "abc-123-def-456",
        "summary": {
          "totalVulnerabilities": 8,
          "criticalCount": 1,
          "highCount": 3,
          "mediumCount": 4,
          "lowCount": 0
        },
        "vulnerabilities": [
          {
            "id": "SNYK-JS-AXIOS-6032459",
            "title": "Server-Side Request Forgery (SSRF)",
            "severity": "high",
            "cvssScore": 7.5,
            "packageName": "axios",
            "packageVersion": "0.21.0",
            "fixedIn": "0.21.1",
            "exploitMaturity": "proof-of-concept",
            "publicationDate": "2023-11-08T00:00:00Z",
            "cve": ["CVE-2023-45857"],
            "cwe": ["CWE-918"],
            "references": [
              "https://snyk.io/vuln/SNYK-JS-AXIOS-6032459"
            ]
          },
          {
            "id": "SNYK-JS-LODASH-1234567",
            "title": "Prototype Pollution",
            "severity": "critical",
            "cvssScore": 9.8,
            "packageName": "lodash",
            "packageVersion": "4.17.19",
            "fixedIn": "4.17.21",
            "exploitMaturity": "mature",
            "publicationDate": "2020-07-15T00:00:00Z",
            "cve": ["CVE-2020-8203"],
            "cwe": ["CWE-1321"],
            "references": [
              "https://snyk.io/vuln/SNYK-JS-LODASH-1234567"
            ]
          }
        ]
      },
      {
        "tool": "prismacloud",
        "toolVersion": "22.12.415",
        "scanDate": "2024-01-15T10:32:00Z",
        "imageId": "sha256:abc123...",
        "imageName": "payment-api:v2.3.1",
        "summary": {
          "totalVulnerabilities": 12,
          "criticalCount": 2,
          "highCount": 5,
          "mediumCount": 5,
          "lowCount": 0
        },
        "vulnerabilities": [
          {
            "id": "CVE-2024-1234",
            "title": "Buffer Overflow in OpenSSL",
            "severity": "critical",
            "cvssScore": 9.1,
            "packageName": "openssl",
            "packageVersion": "1.1.1f",
            "fixedIn": "1.1.1w",
            "cve": ["CVE-2024-1234"],
            "cwe": ["CWE-120"],
            "vector": "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:N",
            "riskFactors": ["Attack complexity: low", "Remote execution"]
          }
        ],
        "complianceIssues": [
          {
            "checkId": "CIS-Docker-1.2.3",
            "severity": "medium",
            "description": "Image should not run as root user",
            "remediation": "Add USER instruction in Dockerfile"
          }
        ]
      }
    ]
  }
}
```

**Input Field Explanations:**

| Field | Type | Description | Source |
|-------|------|-------------|--------|
| `applicationId` | string | Unique application identifier | Application Profile |
| `applicationName` | string | Human-readable app name | Application Profile |
| `riskTier` | string | critical/high/medium/low | Application Profile |
| `environment` | string | production/staging/dev | Request parameter |
| `owner` | string | Team email for notifications | Application Profile |
| `policies` | string[] | OPA policy packages to evaluate | Environment Config |
| `scanResults` | array | Array of tool scan outputs | CI Pipeline |
| `scanResults[].tool` | string | Tool name (snyk, prismacloud) | CI Pipeline |
| `scanResults[].vulnerabilities` | array | List of discovered vulnerabilities | Security Tool |
| `scanResults[].vulnerabilities[].severity` | string | critical/high/medium/low | Security Tool |
| `scanResults[].vulnerabilities[].cvssScore` | number | CVSS 3.x score (0-10) | Security Tool |

**Key Design Decisions:**

1. **Tool-Agnostic Structure**: Each tool's output wrapped in `scanResults` array
2. **Preserved Native Format**: Vulnerability objects keep tool-specific fields
3. **Summary Aggregation**: Pre-calculated counts for quick policy checks
4. **Complete Metadata**: Includes scan timestamp, tool version for audit trail

### OPA Output Structure

OPA evaluates the Rego policy and returns a decision object:

```json
{
  "result": {
    "allow": false,
    "violations": [
      "Critical vulnerabilities (3) exceed maximum (0) for production",
      "High vulnerabilities (8) exceed maximum (0) for production",
      "Compliance check failed: CIS-Docker-1.2.3 - Image runs as root"
    ],
    "details": {
      "criticalCount": 3,
      "highCount": 8,
      "mediumCount": 9,
      "lowCount": 0,
      "totalVulnerabilities": 20,
      "thresholds": {
        "critical": 0,
        "high": 0,
        "medium": 5,
        "low": 10
      },
      "vulnerabilitiesByTool": {
        "snyk": {
          "critical": 1,
          "high": 3,
          "medium": 4
        },
        "prismacloud": {
          "critical": 2,
          "high": 5,
          "medium": 5
        }
      },
      "criticalVulnerabilities": [
        {
          "id": "SNYK-JS-LODASH-1234567",
          "package": "lodash@4.17.19",
          "cvss": 9.8,
          "tool": "snyk"
        },
        {
          "id": "CVE-2024-1234",
          "package": "openssl@1.1.1f",
          "cvss": 9.1,
          "tool": "prismacloud"
        },
        {
          "id": "CVE-2024-5678",
          "package": "nginx@1.19.0",
          "cvss": 9.0,
          "tool": "prismacloud"
        }
      ]
    },
    "evaluatedAt": "2024-01-15T10:33:05Z",
    "policyVersion": "compliance-policies-v2.3.1",
    "evaluationDurationMs": 4
  }
}
```

**Output Field Explanations:**

| Field | Type | Description | Usage |
|-------|------|-------------|-------|
| `result.allow` | boolean | **Primary decision**: true=deploy, false=block | CI pipeline checks this field |
| `result.violations` | string[] | Human-readable violation messages | Display to developers, send to Slack |
| `result.details` | object | Detailed breakdown of evaluation | Audit logging, debugging |
| `result.details.criticalCount` | number | Total critical vulns across all tools | Metrics, dashboards |
| `result.details.thresholds` | object | Applied thresholds from policy | Shows what limits were enforced |
| `result.details.criticalVulnerabilities` | array | List of critical vulns found | Actionable list for developers |
| `result.evaluationDurationMs` | number | OPA evaluation time in milliseconds | Performance monitoring |

### ComplianceService Processing

After receiving OPA's response, ComplianceService processes it as follows:

```csharp
// C# code example - OPA response processing
public class OPAResponse
{
    public OPAResult Result { get; set; }
}

public class OPAResult
{
    public bool Allow { get; set; }
    public List<string> Violations { get; set; } = new();
    public OPADetails Details { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string PolicyVersion { get; set; }
    public int EvaluationDurationMs { get; set; }
}

public async Task<ComplianceEvaluationResponse> EvaluateCompliance(
    string applicationId,
    string environment,
    List<ScanResult> scanResults)
{
    // 1. Retrieve application profile
    var profile = await _applicationRepository.GetByIdAsync(applicationId);

    // 2. Get environment configuration
    var envConfig = await _applicationRepository
        .GetEnvironmentConfigAsync(applicationId, environment);

    // 3. Build OPA input
    var opaInput = new
    {
        input = new
        {
            applicationId = profile.Id,
            applicationName = profile.Name,
            riskTier = profile.RiskTier.ToString().ToLower(),
            environment = environment,
            owner = profile.Owner,
            policies = envConfig.Policies,
            scanResults = scanResults
        }
    };

    // 4. Query OPA sidecar
    var opaResponse = await _opaClient.PostAsync<OPAResponse>(
        "http://localhost:8181/v1/data/compliance/evaluate",
        opaInput
    );

    // 5. Log decision to audit table
    var auditLog = new AuditLog
    {
        EvaluationId = Guid.NewGuid().ToString(),
        ApplicationId = applicationId,
        Environment = environment,
        RiskTier = profile.RiskTier,
        Allowed = opaResponse.Result.Allow,
        Reason = string.Join("; ", opaResponse.Result.Violations),
        Violations = JsonSerializer.Serialize(opaResponse.Result.Violations),
        ScanEvidence = JsonSerializer.Serialize(scanResults),
        EvaluationDurationMs = opaResponse.Result.EvaluationDurationMs,
        CreatedAt = DateTime.UtcNow
    };

    await _auditRepository.InsertAsync(auditLog);

    // 6. Return response to CI pipeline
    return new ComplianceEvaluationResponse
    {
        Allowed = opaResponse.Result.Allow,
        Reason = BuildReason(opaResponse.Result),
        Details = opaResponse.Result.Details,
        EvaluationId = auditLog.EvaluationId,
        Timestamp = DateTime.UtcNow
    };
}

private string BuildReason(OPAResult result)
{
    if (result.Allow)
        return "All compliance checks passed";

    return result.Violations.Count == 1
        ? result.Violations[0]
        : $"{result.Violations.Count} policy violations found: " +
          string.Join(", ", result.Violations.Take(3));
}
```

### Rego Policy Example - Input Processing

Here's how Rego policies access the input structure:

```rego
package compliance.critical_production

import future.keywords.if

# Access application metadata
application_id := input.applicationId
risk_tier := input.riskTier
environment := input.environment

# Count vulnerabilities across ALL tools
all_critical_vulnerabilities contains vuln if {
    scan := input.scanResults[_]  # Iterate all scan results
    vuln := scan.vulnerabilities[_]  # Iterate all vulnerabilities
    vuln.severity == "critical"
}

critical_count := count(all_critical_vulnerabilities)

# Access specific tool results
snyk_results := [scan | scan := input.scanResults[_]; scan.tool == "snyk"]
prisma_results := [scan | scan := input.scanResults[_]; scan.tool == "prismacloud"]

# Build detailed response
decision := result if {
    max_critical := 0  # Threshold defined in policy
    violations := check_violations(critical_count, max_critical)

    result := {
        "allow": count(violations) == 0,
        "violations": violations,
        "details": {
            "criticalCount": critical_count,
            "thresholds": {"critical": max_critical},
            "criticalVulnerabilities": build_vuln_list(all_critical_vulnerabilities)
        },
        "evaluatedAt": time.now_ns(),
        "evaluationDurationMs": 0  # Placeholder
    }
}

build_vuln_list(vulns) := [v |
    vuln := vulns[_];
    v := {
        "id": vuln.id,
        "package": sprintf("%s@%s", [vuln.packageName, vuln.packageVersion]),
        "cvss": vuln.cvssScore,
        "tool": get_tool_for_vuln(vuln)
    }
]
```

**Key OPA Integration Points:**

1. **Input Validation**: Rego policies should validate input structure
2. **Multi-Tool Aggregation**: Count vulnerabilities across all tools
3. **Threshold Application**: Policies define thresholds, not application profiles
4. **Detailed Responses**: Return actionable information in `details` object
5. **Human-Readable Violations**: Format clear messages for developers

### Error Handling

**OPA Connection Failures:**
```csharp
try
{
    var response = await _opaClient.QueryAsync(input);
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Failed to connect to OPA sidecar");
    throw new PolicyEvaluationException(
        "Policy evaluation service unavailable", ex);
}
```

**Policy Evaluation Errors:**
```json
// OPA returns errors in this format
{
  "code": "rego_type_error",
  "message": "undefined reference: max_high",
  "location": {
    "file": "critical-production.rego",
    "row": 25,
    "col": 5
  }
}
```

**Timeout Configuration:**
- OPA query timeout: 10 seconds (should never be reached, typical <10ms)
- HTTP client keeps-alive connections pooled
- Retry policy: 3 retries with exponential backoff (Polly library)

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- PostgreSQL 14+
- OPA binary (deployed as sidecar)
- Docker or Kubernetes for deployment
- CI pipeline configured with Snyk and/or Prisma Cloud
- Git repository for OPA Rego policies

### Installation

#### Local Development

```bash
# Clone the repository
git clone <repository-url>
cd ComplianceService

# Restore dependencies
dotnet restore

# Update database connection string in appsettings.json
# Configure OPA endpoint (localhost:8181 for local sidecar)

# Run database migrations
dotnet ef database update

# Start OPA sidecar
opa run --server --addr localhost:8181 --bundle /path/to/policies &

# Run the application
dotnet run
```

#### Docker Compose (Recommended for Local Dev)

```yaml
version: '3.8'
services:
  complianceservice:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=compliancedb;Username=user;Password=pass
      - OPA__Endpoint=http://opa:8181
    depends_on:
      - postgres
      - opa

  opa:
    image: openpolicyagent/opa:latest
    command:
      - "run"
      - "--server"
      - "--addr=0.0.0.0:8181"
      - "--bundle=/policies"
    volumes:
      - ./policies:/policies
    ports:
      - "8181:8181"

  postgres:
    image: postgres:14
    environment:
      POSTGRES_DB: compliancedb
      POSTGRES_USER: user
      POSTGRES_PASSWORD: pass
    ports:
      - "5432:5432"
```

```bash
docker-compose up
```

### Configuration

Configure the following in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=compliancedb;Username=user;Password=pass"
  },
  "OPA": {
    "Endpoint": "http://localhost:8181",
    "PolicyPath": "/v1/data/compliance/evaluate"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Notes:**
- **OPA Sidecar**: OPA runs on localhost as a sidecar container (typically port 8181)
- **No Security Tool Credentials**: CI pipeline executes tools and forwards JSON outputs
- **Policy Repository**: OPA loads Rego policies from Git repository on startup

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

Register an application with its environments, tools, and policy mappings:

```bash
POST /api/applications
Content-Type: application/json

{
  "name": "my-payment-app",
  "riskTier": "critical",
  "owner": "payments-team",
  "environments": [
    {
      "name": "production",
      "securityTools": ["snyk", "prismacloud"],
      "policies": [
        "compliance/critical-production",
        "compliance/zero-critical-vulns"
      ],
      "metadata": {
        "snykProjectId": "abc-123",
        "prismaProjectId": "xyz-789"
      }
    },
    {
      "name": "staging",
      "securityTools": ["snyk", "prismacloud"],
      "policies": [
        "compliance/critical-staging",
        "compliance/limited-high-vulns"
      ],
      "metadata": {
        "snykProjectId": "abc-456",
        "prismaProjectId": "xyz-012"
      }
    },
    {
      "name": "dev",
      "securityTools": ["snyk"],
      "policies": [
        "compliance/dev-relaxed"
      ],
      "metadata": {
        "snykProjectId": "abc-789"
      }
    }
  ]
}
```

**Key Points:**
- **No thresholds** in application profile - thresholds are defined in OPA policies
- **Tools per environment** - Different environments can use different security tools
- **Policies per environment** - Map to OPA policy packages
- **Metadata only** - Tool configuration for reference, not for direct integration
- **CI pipeline responsibility** - Executes tools and sends outputs to ComplianceService

## OPA Policy Examples

### Policy Structure

Policies are organized by environment and purpose. **All thresholds are defined in policies, NOT in application profiles.**

#### Policy 1: `compliance/critical-production.rego`

Zero tolerance for critical applications in production:

```rego
package compliance.critical_production

import future.keywords.if

# Default deny
default allow := false
default decision := {
    "allow": false,
    "violations": [],
    "details": {}
}

# Main evaluation rule
decision := result if {
    # Count vulnerabilities by severity across ALL security tools
    critical_count := count(all_critical_vulnerabilities)
    high_count := count(all_high_vulnerabilities)
    medium_count := count(all_medium_vulnerabilities)

    # THRESHOLDS DEFINED HERE - not in application profile
    max_critical := 0
    max_high := 0
    max_medium := 5

    violations := check_thresholds(critical_count, high_count, medium_count, max_critical, max_high, max_medium)

    result := {
        "allow": count(violations) == 0,
        "violations": violations,
        "details": {
            "criticalCount": critical_count,
            "highCount": high_count,
            "mediumCount": medium_count,
            "thresholds": {
                "critical": max_critical,
                "high": max_high,
                "medium": max_medium
            }
        }
    }
}

# Aggregate vulnerabilities from all security tools
all_critical_vulnerabilities contains vuln if {
    scan := input.scanResults[_]
    vuln := scan.vulnerabilities[_]
    vuln.severity == "critical"
}

all_high_vulnerabilities contains vuln if {
    scan := input.scanResults[_]
    vuln := scan.vulnerabilities[_]
    vuln.severity == "high"
}

all_medium_vulnerabilities contains vuln if {
    scan := input.scanResults[_]
    vuln := scan.vulnerabilities[_]
    vuln.severity == "medium"
}

# Check threshold violations
check_thresholds(critical, high, medium, max_c, max_h, max_m) := violations if {
    violations := array.concat(
        check_critical(critical, max_c),
        array.concat(
            check_high(high, max_h),
            check_medium(medium, max_m)
        )
    )
}

check_critical(count, max) := [msg] if {
    count > max
    msg := sprintf("Critical vulnerabilities (%d) exceed maximum (%d)", [count, max])
} else := []

check_high(count, max) := [msg] if {
    count > max
    msg := sprintf("High vulnerabilities (%d) exceed maximum (%d)", [count, max])
} else := []

check_medium(count, max) := [msg] if {
    count > max
    msg := sprintf("Medium vulnerabilities (%d) exceed maximum (%d)", [count, max])
} else := []
```

#### Policy 2: `compliance/critical-staging.rego`

Relaxed thresholds for staging environments:

```rego
package compliance.critical_staging

import future.keywords.if

default decision := {
    "allow": false,
    "violations": [],
    "details": {}
}

decision := result if {
    critical_count := count(all_critical_vulnerabilities)
    high_count := count(all_high_vulnerabilities)

    # Different thresholds for staging
    max_critical := 2
    max_high := 5

    violations := []
    violations := array.concat(violations, check_critical(critical_count, max_critical))
    violations := array.concat(violations, check_high(high_count, max_high))

    result := {
        "allow": count(violations) == 0,
        "violations": violations,
        "details": {
            "criticalCount": critical_count,
            "highCount": high_count,
            "environment": "staging"
        }
    }
}

# ... (vulnerability aggregation rules same as above)
```

### Input Structure

ComplianceService sends this structure to OPA:

```json
{
  "applicationId": "my-payment-app",
  "riskTier": "critical",
  "environment": "production",
  "policies": ["compliance/critical-production", "compliance/zero-critical-vulns"],
  "scanResults": [
    {
      "tool": "snyk",
      "scanDate": "2024-01-15T10:30:00Z",
      "vulnerabilities": [
        {
          "id": "SNYK-JS-AXIOS-1234567",
          "severity": "high",
          "packageName": "axios",
          "version": "0.21.0"
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

**Key Principles:**
- **Thresholds in policies** - Application profiles only reference policy names
- **Environment-specific policies** - Different thresholds for dev/staging/production
- **Multi-tool aggregation** - Policies count vulnerabilities across all security tools
- **Risk-tier aware** - Policies can check risk tier for additional logic

## Development

### Project Structure

```
ComplianceService/
├── src/
│   ├── Domain/                          # Domain models and logic
│   │   ├── ApplicationProfile/          # Application aggregate
│   │   ├── Evaluation/                  # Evaluation aggregate
│   │   └── Audit/                       # Audit aggregate
│   ├── Application/                     # Use cases and orchestration
│   ├── Infrastructure/                  # Data access, external services
│   │   ├── Persistence/                 # PostgreSQL repositories
│   │   └── OPA/                         # OPA client integration
│   └── API/                             # Controllers and API configuration
├── tests/
│   ├── UnitTests/
│   ├── IntegrationTests/
│   └── ArchitectureTests/
├── k8s/                                 # Kubernetes manifests
│   ├── deployment.yaml                  # ComplianceService + OPA sidecar
│   ├── service.yaml
│   └── configmap.yaml
└── docker-compose.yml                   # Local development setup

Separate Policy Repository:
compliance-policies/
├── compliance/
│   ├── critical-production.rego         # Zero tolerance policy
│   ├── critical-staging.rego            # Limited tolerance policy
│   ├── dev-relaxed.rego                 # Development policy
│   └── common/
│       ├── vulnerability-helpers.rego   # Reusable functions
│       └── severity-aggregation.rego    # Counting utilities
└── tests/
    └── compliance_test.rego             # Policy unit tests
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

### Kubernetes Deployment (Recommended)

Deploy ComplianceService with OPA as a sidecar:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: complianceservice
spec:
  replicas: 3
  selector:
    matchLabels:
      app: complianceservice
  template:
    metadata:
      labels:
        app: complianceservice
    spec:
      containers:
      # Main application container
      - name: api
        image: complianceservice:latest
        ports:
        - containerPort: 5000
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: complianceservice-secrets
              key: db-connection
        - name: OPA__Endpoint
          value: "http://localhost:8181"
        - name: OPA__PolicyPath
          value: "/v1/data/compliance/evaluate"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"

      # OPA sidecar container
      - name: opa
        image: openpolicyagent/opa:latest
        ports:
        - containerPort: 8181
        args:
        - "run"
        - "--server"
        - "--addr=0.0.0.0:8181"
        - "--set=bundles.complianceservice.service=bundle-server"
        - "--set=bundles.complianceservice.resource=/bundles/compliance.tar.gz"
        volumeMounts:
        - name: policy-bundle
          mountPath: /policies
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"

      volumes:
      - name: policy-bundle
        configMap:
          name: opa-policies
```

### Policy Updates

Policies can be updated without redeploying the service:

**Option 1: Git-based Policy Bundle (Recommended)**
1. Update Rego policies in Git repository
2. OPA polls Git for policy changes
3. Policies reload automatically within 30 seconds
4. No pod restart required

**Option 2: ConfigMap Update**
```bash
# Update policies ConfigMap
kubectl create configmap opa-policies --from-file=./policies --dry-run=client -o yaml | kubectl apply -f -

# Restart OPA sidecar to load new policies
kubectl rollout restart deployment complianceservice
```

### Service Deployment

```bash
# Build for production
dotnet publish -c Release

# Build Docker image
docker build -t complianceservice:latest .

# Push to registry
docker push your-registry/complianceservice:latest

# Deploy to Kubernetes
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

### Health Checks

```yaml
# Add to deployment spec
livenessProbe:
  httpGet:
    path: /health
    port: 5000
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /ready
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 5
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
