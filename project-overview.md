# ComplianceService - Project Overview

## Table of Contents

- [1. Executive Summary](#1-executive-summary)
- [2. Problem Statement](#2-problem-statement)
- [3. Solution Overview](#3-solution-overview)
- [4. System Architecture](#4-system-architecture)
  - [4.1 High-Level Architecture Diagram](#41-high-level-architecture-diagram)
  - [4.2 Component Breakdown](#42-component-breakdown)
  - [4.3 Internal Layer Architecture](#43-internal-layer-architecture)
- [5. Service Flow - End to End](#5-service-flow---end-to-end)
  - [5.1 CI/CD Pipeline Integration Flow](#51-cicd-pipeline-integration-flow)
  - [5.2 Compliance Evaluation Workflow (Core Flow)](#52-compliance-evaluation-workflow-core-flow)
  - [5.3 Application Registration Flow](#53-application-registration-flow)
  - [5.4 Audit and Reporting Flow](#54-audit-and-reporting-flow)
- [6. OPA Policy Engine - Policy-as-Code](#6-opa-policy-engine---policy-as-code)
  - [6.1 OPA Integration Architecture](#61-opa-integration-architecture)
  - [6.2 Policy Hierarchy by Environment](#62-policy-hierarchy-by-environment)
  - [6.3 Policy Evaluation Data Flow](#63-policy-evaluation-data-flow)
- [7. Domain Model](#7-domain-model)
  - [7.1 Bounded Contexts](#71-bounded-contexts)
  - [7.2 Risk Tier Classification](#72-risk-tier-classification)
- [8. API Surface](#8-api-surface)
  - [8.1 Compliance Endpoints](#81-compliance-endpoints)
  - [8.2 Application Management Endpoints](#82-application-management-endpoints)
  - [8.3 Audit and Reporting Endpoints](#83-audit-and-reporting-endpoints)
  - [8.4 Health and Observability](#84-health-and-observability)
- [9. CI/CD Pipeline Integration](#9-cicd-pipeline-integration)
  - [9.1 Pipeline Gate Diagram](#91-pipeline-gate-diagram)
  - [9.2 Integration Points](#92-integration-points)
- [10. Infrastructure and Deployment](#10-infrastructure-and-deployment)
  - [10.1 Container Topology](#101-container-topology)
  - [10.2 Data Persistence](#102-data-persistence)
- [11. Security and Governance](#11-security-and-governance)
  - [11.1 Security Controls](#111-security-controls)
  - [11.2 Governance Framework](#112-governance-framework)
  - [11.3 Compliance Audit Trail](#113-compliance-audit-trail)
  - [11.4 Threat Model Considerations](#114-threat-model-considerations)
- [12. Scalability and Growth](#12-scalability-and-growth)
  - [12.1 Current Architecture Scalability](#121-current-architecture-scalability)
  - [12.2 Horizontal Scaling Strategy](#122-horizontal-scaling-strategy)
  - [12.3 Growth Roadmap](#123-growth-roadmap)
  - [12.4 Performance Considerations](#124-performance-considerations)
- [13. Evaluation and Maturity Assessment](#13-evaluation-and-maturity-assessment)
  - [13.1 Current State Assessment](#131-current-state-assessment)
  - [13.2 Gap Analysis](#132-gap-analysis)
  - [13.3 Recommended Next Steps](#133-recommended-next-steps)
- [14. Technology Stack](#14-technology-stack)

---

## 1. Executive Summary

ComplianceService is a **policy gateway for CI/CD pipelines** that acts as an automated compliance checkpoint between security scanning tools and deployment targets. It receives security scan results from tools like Snyk and Prisma Cloud, evaluates them against organizational compliance policies using Open Policy Agent (OPA), and returns an **allow/deny decision** that gates whether a deployment can proceed.

The service solves a fundamental DevSecOps challenge: enforcing consistent, auditable, policy-driven security compliance across all application deployments without manual intervention.

---

## 2. Problem Statement

Organizations deploying software at scale face several interconnected challenges:

| Challenge | Description |
|-----------|-------------|
| **Inconsistent enforcement** | Security policies are applied manually or inconsistently across teams and environments |
| **No audit trail** | Deployment decisions lack traceable evidence for regulatory or internal compliance |
| **Slow manual gates** | Human security review creates deployment bottlenecks |
| **Environment drift** | Production, staging, and development have different security standards that are not codified |
| **Tool fragmentation** | Multiple security scanners (Snyk, Prisma Cloud, SonarQube) produce results in different formats with no unified evaluation |
| **Risk blindness** | No centralized view of vulnerability posture across the application portfolio |

Without an automated compliance gate, organizations must choose between deployment velocity and security posture. ComplianceService eliminates this trade-off.

---

## 3. Solution Overview

ComplianceService provides:

1. **Automated Policy Evaluation** - Security scan results are evaluated against Rego policies in OPA with zero human intervention
2. **Environment-Aware Decisions** - Different policies apply to production (zero tolerance) vs. development (permissive) environments
3. **Risk-Tiered Classification** - Applications are categorized as critical, high, medium, or low risk, each with appropriate policy strictness
4. **Immutable Audit Trail** - Every evaluation decision is recorded with full evidence (scan inputs, policy inputs, policy outputs) for regulatory compliance
5. **Pipeline Integration** - A single REST API call from any CI/CD pipeline (GitHub Actions, Azure DevOps, Jenkins) returns a go/no-go decision
6. **Centralized Reporting** - Dashboards for blocked deployments, critical vulnerabilities, and compliance statistics across the portfolio

---

## 4. System Architecture

### 4.1 High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              CI/CD PIPELINES                                    │
│  ┌──────────────┐  ┌──────────────────┐  ┌─────────────┐  ┌─────────────────┐  │
│  │GitHub Actions │  │Azure DevOps      │  │Jenkins      │  │GitLab CI        │  │
│  └──────┬───────┘  └────────┬─────────┘  └──────┬──────┘  └────────┬────────┘  │
│         │                   │                    │                   │           │
│         │    ┌──────────────┴────────────────────┴───────────────────┘           │
│         │    │  Security Scan Results (Snyk, Prisma Cloud, SonarQube)            │
│         └────┤                                                                  │
│              │  POST /api/compliance/evaluate                                   │
└──────────────┼──────────────────────────────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                        COMPLIANCE SERVICE (API)                                  │
│                                                                                  │
│  ┌─────────────────┐    ┌─────────────────┐    ┌──────────────────────────────┐  │
│  │  Request         │    │  Compliance      │    │  Audit                       │  │
│  │  Logging         │───▶│  Controller      │───▶│  Controller                  │  │
│  │  Middleware       │    │                  │    │                              │  │
│  └─────────────────┘    └────────┬─────────┘    └──────────────────────────────┘  │
│                                  │                                                │
│                    ┌─────────────▼──────────────┐                                │
│                    │  EvaluateCompliance         │                                │
│                    │  CommandHandler (MediatR)   │                                │
│                    └─────────────┬──────────────┘                                │
│                                  │                                                │
│          ┌───────────────────────┼───────────────────────┐                       │
│          │                       │                       │                       │
│          ▼                       ▼                       ▼                       │
│  ┌───────────────┐    ┌──────────────────┐    ┌──────────────────┐              │
│  │ Application    │    │  OPA HTTP        │    │  Audit Log       │              │
│  │ Repository     │    │  Client          │    │  Repository      │              │
│  └───────┬───────┘    └────────┬─────────┘    └────────┬─────────┘              │
│          │                     │                        │                        │
└──────────┼─────────────────────┼────────────────────────┼────────────────────────┘
           │                     │                        │
           ▼                     ▼                        ▼
┌──────────────────┐  ┌──────────────────┐    ┌──────────────────┐
│   PostgreSQL     │  │   OPA Sidecar    │    │   PostgreSQL     │
│   (App Data +    │  │   (Rego Policy   │    │   (Audit Logs)   │
│    Evaluations)  │  │    Engine)       │    │                  │
└──────────────────┘  └────────┬─────────┘    └──────────────────┘
                               │
                      ┌────────┴──────────┐
                      │  Rego Policies    │
                      │  ┌──────────────┐ │
                      │  │ production   │ │
                      │  │ staging      │ │
                      │  │ development  │ │
                      │  │ common       │ │
                      │  └──────────────┘ │
                      └───────────────────┘
```

### 4.2 Component Breakdown

| Component | Role | Technology |
|-----------|------|------------|
| **ComplianceService API** | REST gateway; receives scan results, orchestrates evaluation, returns decisions | ASP.NET Core 8.0 |
| **OPA Sidecar** | Stateless policy evaluation engine; executes Rego policies against structured input | Open Policy Agent 0.60+ |
| **PostgreSQL** | Persistent store for application profiles, evaluation records, and audit logs | PostgreSQL 16 |
| **Rego Policies** | Declarative compliance rules versioned alongside the service | OPA Rego language |
| **Security Scanners** | External tools (Snyk, Prisma Cloud, SonarQube) that produce vulnerability scan results | Third-party |
| **CI/CD Pipelines** | Callers of the service; submit scan results and gate deployments on the response | GitHub Actions, Azure DevOps, etc. |

### 4.3 Internal Layer Architecture

The service follows **Clean Architecture** with Domain-Driven Design:

```
┌───────────────────────────────────────────────────────────────────┐
│                        API Layer                                  │
│  Controllers  │  Middleware  │  Program.cs (Startup)              │
│  (HTTP in/out)│  (Logging,  │  (DI, Health Checks, Swagger)      │
│               │  Exceptions)│                                     │
├───────────────────────────────────────────────────────────────────┤
│                     Application Layer                             │
│  Commands  │  Queries  │  Handlers  │  Validators  │  DTOs       │
│  (CQRS Write)│(CQRS Read)│(MediatR)  │(FluentValid) │(Contracts) │
│               │           │           │              │            │
│  Interfaces: IOpaClient, INotificationService                    │
├───────────────────────────────────────────────────────────────────┤
│                       Domain Layer                                │
│  Aggregates:                                                      │
│  ┌─────────────────┐ ┌──────────────────┐ ┌──────────────┐       │
│  │ Application      │ │ ComplianceEval   │ │ AuditLog     │       │
│  │ (Aggregate Root) │ │ (Aggregate Root) │ │ (Agg. Root)  │       │
│  │ + EnvironmentCfg │ │ + ScanResult     │ │ + Evidence   │       │
│  │ + RiskTier       │ │ + PolicyDecision │ │ + Violations │       │
│  │ + PolicyRef      │ │ + Vulnerability  │ │ + Counts     │       │
│  └─────────────────┘ └──────────────────┘ └──────────────┘       │
├───────────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                            │
│  Persistence:                  │  External Services:              │
│  ┌──────────────────────┐      │  ┌─────────────────────┐        │
│  │ ApplicationDbContext  │      │  │ OpaHttpClient       │        │
│  │ (EF Core + Npgsql)   │      │  │ (HTTP → OPA)        │        │
│  │                       │      │  ├─────────────────────┤        │
│  │ Repositories:         │      │  │ LoggingNotification │        │
│  │ - Application         │      │  │ Service             │        │
│  │ - ComplianceEvaluation│      │  └─────────────────────┘        │
│  │ - AuditLog            │      │                                 │
│  └──────────────────────┘      │                                  │
└───────────────────────────────────────────────────────────────────┘
```

---

## 5. Service Flow - End to End

### 5.1 CI/CD Pipeline Integration Flow

This is the primary use case: a CI/CD pipeline calls ComplianceService as a deployment gate.

```
Developer pushes code
        │
        ▼
┌──────────────────────┐
│  CI Pipeline Starts  │
│  (Build, Test)       │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│  Security Scans Run  │
│  ┌────────────────┐  │
│  │ Snyk (SCA)     │  │    Scans dependencies for known CVEs
│  │ Prisma Cloud   │  │    Scans container images and IaC
│  │ SonarQube      │  │    Static analysis for code quality
│  └────────────────┘  │
└──────────┬───────────┘
           │  Scan results collected
           ▼
┌──────────────────────────────────────────┐
│  POST /api/compliance/evaluate           │
│  {                                       │
│    "applicationId": "<guid>",            │
│    "environment": "production",          │
│    "scanResults": [ ... ],               │
│    "initiatedBy": "ci-pipeline@org.com", │
│    "metadata": { "pipelineId": "..." }   │
│  }                                       │
└──────────────────┬───────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────┐
│  ComplianceService evaluates...          │
│  (see Section 5.2 for details)           │
└──────────────────┬───────────────────────┘
                   │
          ┌────────┴────────┐
          │                 │
          ▼                 ▼
   ┌─────────────┐  ┌──────────────┐
   │   ALLOW     │  │    DENY      │
   │  (passed:   │  │  (passed:    │
   │   true)     │  │   false)     │
   └──────┬──────┘  └──────┬───────┘
          │                │
          ▼                ▼
┌──────────────┐  ┌──────────────────┐
│  Deploy to   │  │  Pipeline Fails  │
│  Target Env  │  │  Notify Owner    │
└──────────────┘  │  Log Violations  │
                  └──────────────────┘
```

### 5.2 Compliance Evaluation Workflow (Core Flow)

This is the 9-step internal process that occurs when `POST /api/compliance/evaluate` is called:

```
                    EvaluateComplianceCommandHandler
                    ════════════════════════════════

Step 1 ─── Lookup Application + Environment Config
           │
           │  Query PostgreSQL for application profile
           │  Validate application is active
           │  Retrieve environment-specific config (risk tier, policies, tools)
           │
           ▼
Step 2 ─── Convert Scan Results to Domain Objects
           │
           │  Parse each scan result (Snyk, Prisma, etc.)
           │  Build Vulnerability value objects with:
           │    - CVE ID, severity, CVSS score
           │    - Package name, current version, fixed version
           │  Assemble ScanResult aggregates
           │
           ▼
Step 3 ─── Construct OPA Input Payload
           │
           │  Build structured input:
           │  {
           │    "application": { name, environment, riskTier, owner },
           │    "scanResults": [ { tool, vulnerabilities, counts } ],
           │    "metadata": { ... }
           │  }
           │
           ▼
Step 4 ─── Call OPA Sidecar for Policy Decision
           │
           │  HTTP POST → http://opa:8181/v1/data/{policy-package}
           │  e.g., /v1/data/compliance/cicd/production
           │
           │  OPA evaluates Rego rules and returns:
           │  {
           │    "allow": true/false,
           │    "violations": [ { rule, message, severity } ],
           │    "reason": "..."
           │  }
           │
           ▼
Step 5 ─── Create PolicyDecision Domain Value Object
           │
           │  Map OPA response to domain model
           │  Validate: denied decisions must have ≥ 1 violation
           │
           ▼
Step 6 ─── Create ComplianceEvaluation Aggregate
           │
           │  Persist evaluation record to PostgreSQL
           │  Includes: applicationId, environment, riskTier,
           │            scanResults, policyDecision, timestamp
           │
           ▼
Step 7 ─── Create Immutable Audit Log
           │
           │  Store DecisionEvidence containing:
           │    - Raw scan results JSON
           │    - Exact OPA input JSON
           │    - Exact OPA output JSON
           │  Store aggregated vulnerability counts
           │  Persist to PostgreSQL
           │
           ▼
Step 8 ─── Fire Notifications (async, non-blocking)
           │
           │  If BLOCKED or critical vulnerabilities found:
           │    - Notify application owner
           │    - Send critical vulnerability alert
           │  (Currently logs; extensible to Slack, email, PagerDuty)
           │
           ▼
Step 9 ─── Return ComplianceEvaluationDto
           │
           │  Response includes:
           │    - passed: true/false
           │    - policyDecision: { allow, violations[], reason }
           │    - aggregatedCounts: { critical, high, medium, low }
           │    - scanResults with full vulnerability details
           │
           ▼
        Pipeline receives decision and continues or halts
```

### 5.3 Application Registration Flow

Before an application can be evaluated, it must be registered with its environment configurations:

```
Step 1: Register Application
        POST /api/applications
        { "name": "payment-service", "owner": "team-lead@company.com" }
            │
            ▼
Step 2: Add Environment Configuration
        POST /api/applications/{id}/environments
        {
          "environmentName": "production",
          "riskTier": "critical",
          "securityTools": ["snyk", "prismacloud"],
          "policies": ["compliance.cicd.production"]
        }
            │
            ▼
Step 3: (Repeat for each environment)
        POST /api/applications/{id}/environments
        { "environmentName": "staging", "riskTier": "high", ... }
        { "environmentName": "dev", "riskTier": "low", ... }
            │
            ▼
        Application is now ready for compliance evaluations
```

### 5.4 Audit and Reporting Flow

The audit system provides full traceability for compliance officers and security teams:

```
┌──────────────────────────────────────────────────────────┐
│                    AUDIT QUERIES                          │
│                                                          │
│  GET /api/audit/statistics                               │
│  ├── Total evaluations, allowed vs blocked               │
│  ├── Blocked percentage                                  │
│  ├── Breakdown by environment                            │
│  └── Breakdown by risk tier                              │
│                                                          │
│  GET /api/audit/blocked?days=30                           │
│  └── All denied deployments in last 30 days              │
│                                                          │
│  GET /api/audit/critical-vulnerabilities?days=7           │
│  └── All evaluations with critical CVEs                  │
│                                                          │
│  GET /api/audit/application/{id}                          │
│  └── Full audit history for a specific application       │
│                                                          │
│  GET /api/audit/risk-tier/critical                        │
│  └── All evaluations for critical-tier applications      │
│                                                          │
│  GET /api/audit/{id}                                      │
│  └── Single audit record with full evidence JSON         │
│      (scan input + OPA input + OPA output)               │
└──────────────────────────────────────────────────────────┘
```

---

## 6. OPA Policy Engine - Policy-as-Code

### 6.1 OPA Integration Architecture

ComplianceService uses OPA as a **sidecar service** deployed alongside the API. OPA runs its own HTTP server on port 8181 and loads Rego policy files from a mounted volume.

```
┌──────────────────────────┐         ┌──────────────────────────────────┐
│  ComplianceService API   │         │  OPA Sidecar                     │
│                          │  HTTP   │                                  │
│  OpaHttpClient ─────────────────▶  │  /v1/data/{package}              │
│                          │  POST   │                                  │
│  Constructs:             │         │  Evaluates:                      │
│  {                       │         │  ┌──────────────────────────┐    │
│    "input": {            │         │  │ Rego Policy Files        │    │
│      "application":...,  │         │  │ /policies/compliance/    │    │
│      "scanResults":...,  │         │  │   cicd/production.rego   │    │
│      "metadata":...      │         │  │   cicd/staging.rego      │    │
│    }                     │         │  │   cicd/development.rego  │    │
│  }                       │         │  │   cicd/common.rego       │    │
│                          │         │  └──────────────────────────┘    │
│  Receives:               │         │                                  │
│  {                       │         │  Returns:                        │
│    "result": {           │◀────────│  { allow, violations[], reason } │
│      "allow": bool,      │         │                                  │
│      "violations": [...] │         │                                  │
│    }                     │         │                                  │
│  }                       │         │                                  │
└──────────────────────────┘         └──────────────────────────────────┘
```

### 6.2 Policy Hierarchy by Environment

Each environment enforces a different policy strictness level:

```
                    POLICY STRICTNESS
                    ─────────────────

    PRODUCTION (compliance.cicd.production)
    ┌────────────────────────────────────────────────────┐
    │  ZERO TOLERANCE                                     │
    │  ✗ 0 critical vulnerabilities                       │
    │  ✗ 0 high vulnerabilities                           │
    │  ✗ 0 high-severity license violations               │
    │  ✓ All 3 tools required: Snyk + Prisma + SonarQube  │
    │  Default: DENY                                       │
    └────────────────────────────────────────────────────┘
                        │
                        ▼
    STAGING (compliance.cicd.staging)
    ┌────────────────────────────────────────────────────┐
    │  MODERATE TOLERANCE                                  │
    │  ✗ 0 critical vulnerabilities                       │
    │  ⚠ Up to 3 high vulnerabilities allowed             │
    │  ✓ 2 tools required: Snyk + Prisma Cloud            │
    │  Default: DENY                                       │
    └────────────────────────────────────────────────────┘
                        │
                        ▼
    DEVELOPMENT (compliance.cicd.development)
    ┌────────────────────────────────────────────────────┐
    │  PERMISSIVE (tracking mode)                          │
    │  ⚠ Up to 5 critical vulnerabilities allowed         │
    │  ✓ No tool requirements                              │
    │  ⚠ Warning if no scans run at all                    │
    │  Default: ALLOW                                      │
    └────────────────────────────────────────────────────┘
```

### 6.3 Policy Evaluation Data Flow

```
OPA Input (what the API sends)          OPA Output (what OPA returns)
═══════════════════════════             ══════════════════════════════

{                                       {
  "input": {                              "result": {
    "application": {                        "allow": false,
      "name": "payment-svc",               "violations": [
      "environment": "production",            {
      "riskTier": "critical",                   "rule": "no_critical_vulns",
      "owner": "team@co.com"                    "message": "Found: 2 critical",
    },                                          "severity": "critical"
    "scanResults": [                          }
      {                                     ],
        "tool": "snyk",                     "reason": "Production compliance
        "criticalCount": 2,                           violations detected"
        "highCount": 0,                   }
        "vulnerabilities": [...]        }
      }
    ]
  }
}
```

---

## 7. Domain Model

### 7.1 Bounded Contexts

The service is organized into three distinct bounded contexts following DDD principles:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│   APPLICATION PROFILE           EVALUATION               AUDIT          │
│   CONTEXT                       CONTEXT                  CONTEXT        │
│                                                                         │
│  ┌───────────────────┐    ┌────────────────────┐   ┌────────────────┐  │
│  │  Application       │    │ ComplianceEval     │   │ AuditLog       │  │
│  │  (Aggregate Root)  │    │ (Aggregate Root)   │   │ (Aggregate     │  │
│  │                    │    │                    │   │  Root)         │  │
│  │  - Name            │    │  - ApplicationId   │   │                │  │
│  │  - Owner (email)   │◄───│  - Environment     │──▶│ - EvaluationId │  │
│  │  - IsActive        │    │  - RiskTier        │   │ - AppName      │  │
│  │                    │    │  - EvaluatedAt     │   │ - Allowed      │  │
│  │  EnvironmentConfig │    │                    │   │ - Reason       │  │
│  │  ┌──────────────┐  │    │  ScanResult[]      │   │ - Violations[] │  │
│  │  │- Env Name    │  │    │  ┌──────────────┐  │   │                │  │
│  │  │- RiskTier    │  │    │  │- Tool        │  │   │ DecisionEvid.  │  │
│  │  │- SecurityTool│  │    │  │- ScanDate    │  │   │ ┌────────────┐ │  │
│  │  │- Policies[]  │  │    │  │- Vulns[]     │  │   │ │ScanResults │ │  │
│  │  │- Metadata    │  │    │  └──────────────┘  │   │ │ JSON       │ │  │
│  │  └──────────────┘  │    │                    │   │ │PolicyInput │ │  │
│  │                    │    │  PolicyDecision     │   │ │ JSON       │ │  │
│  │  PolicyReference   │    │  ┌──────────────┐  │   │ │PolicyOutput│ │  │
│  │  ┌──────────────┐  │    │  │- Allowed     │  │   │ │ JSON       │ │  │
│  │  │- PackageName │  │    │  │- Violations[]│  │   │ └────────────┘ │  │
│  │  └──────────────┘  │    │  │- Duration    │  │   │                │  │
│  │                    │    │  └──────────────┘  │   │ Vuln Counts:   │  │
│  │  RiskTier (VO)     │    │                    │   │ critical/high/ │  │
│  │  critical│high│    │    │  Vulnerability     │   │ medium/low     │  │
│  │  medium  │low │    │    │  ┌──────────────┐  │   │                │  │
│  └───────────────────┘    │  │- CVE ID      │  │   └────────────────┘  │
│                            │  │- Severity    │  │                       │
│                            │  │- CVSS Score  │  │                       │
│                            │  │- Package     │  │                       │
│                            │  │- FixedIn     │  │                       │
│                            │  └──────────────┘  │                       │
│                            └────────────────────┘                       │
└─────────────────────────────────────────────────────────────────────────┘
```

### 7.2 Risk Tier Classification

Risk tiers determine which policies and enforcement levels apply:

| Risk Tier | Typical Use | Policy Strictness | Required Tools | Example |
|-----------|------------|-------------------|----------------|---------|
| **Critical** | Customer-facing, PCI/HIPAA regulated | Zero tolerance for critical + high vulns | Snyk, Prisma Cloud, SonarQube | Payment processing |
| **High** | Internal business-critical services | Zero critical; up to 3 high allowed | Snyk, Prisma Cloud | HR management system |
| **Medium** | Internal tools and services | Moderate tolerance | Snyk | Internal dashboards |
| **Low** | Development utilities, experiments | Tracking only; rarely blocks | None required | Developer tooling |

---

## 8. API Surface

### 8.1 Compliance Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `POST` | `/api/compliance/evaluate` | **Core endpoint** - Evaluate scan results against policies, return allow/deny |
| `GET` | `/api/compliance/{id}` | Retrieve a specific evaluation by ID |
| `GET` | `/api/compliance/application/{appId}` | Get evaluations for an application (filterable by environment, days) |
| `GET` | `/api/compliance/recent?days=7` | Get recent evaluations across all applications |
| `GET` | `/api/compliance/blocked?days=30` | Get all denied deployments |

### 8.2 Application Management Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `POST` | `/api/applications` | Register a new application |
| `GET` | `/api/applications/{id}` | Get application by ID |
| `GET` | `/api/applications/by-name/{name}` | Get application by name |
| `GET` | `/api/applications?owner=&pageNumber=&pageSize=` | List all applications (paginated, filterable) |
| `PATCH` | `/api/applications/{id}/owner` | Update application owner |
| `POST` | `/api/applications/{id}/deactivate` | Deactivate an application |
| `POST` | `/api/applications/{id}/environments` | Add environment configuration |
| `PUT` | `/api/applications/{id}/environments/{env}` | Update environment configuration |
| `POST` | `/api/applications/{id}/environments/{env}/deactivate` | Deactivate an environment |

### 8.3 Audit and Reporting Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/audit/{id}` | Get audit log by ID (includes full evidence JSON) |
| `GET` | `/api/audit/evaluation/{evaluationId}` | Get audit log by evaluation ID |
| `GET` | `/api/audit/application/{appId}` | Get audit logs for an application (paginated, date-filterable) |
| `GET` | `/api/audit/blocked?days=&limit=` | Get blocked (denied) decisions |
| `GET` | `/api/audit/critical-vulnerabilities?days=` | Get evaluations with critical vulnerabilities |
| `GET` | `/api/audit/risk-tier/{tier}` | Get audit logs by risk tier |
| `GET` | `/api/audit/statistics?fromDate=&toDate=` | Get aggregate compliance statistics |

### 8.4 Health and Observability

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Health check endpoint (checks PostgreSQL + OPA sidecar connectivity) |
| `GET /swagger` | OpenAPI documentation (development environment only) |

Health check response includes status of both dependencies:

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgresql", "status": "Healthy", "duration": 12.5 },
    { "name": "opa-sidecar", "status": "Healthy", "duration": 3.2 }
  ],
  "totalDuration": 15.7
}
```

---

## 9. CI/CD Pipeline Integration

### 9.1 Pipeline Gate Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│                       CI/CD PIPELINE                                     │
│                                                                          │
│  ┌──────┐    ┌──────┐    ┌───────────┐    ┌─────────────┐    ┌───────┐  │
│  │ Code │───▶│Build │───▶│   Test    │───▶│  Security   │───▶│Compli-│  │
│  │ Push │    │      │    │ (Unit +   │    │  Scans      │    │ance   │  │
│  │      │    │      │    │  Integr.) │    │ (Snyk,      │    │ Gate  │  │
│  └──────┘    └──────┘    └───────────┘    │  Prisma,    │    │       │  │
│                                           │  SonarQube) │    │ POST  │  │
│                                           └──────┬──────┘    │/eval- │  │
│                                                  │           │uate   │  │
│                                    Scan results  │           │       │  │
│                                    collected ────┘           │       │  │
│                                                              └───┬───┘  │
│                                                                  │      │
│                                                         ┌────────┴───┐  │
│                                                         │            │  │
│                                                    ALLOW ▼       DENY ▼  │
│                                                   ┌────────┐  ┌───────┐ │
│                                                   │Deploy  │  │ STOP  │ │
│                                                   │to Env  │  │ Pipeline│ │
│                                                   └────────┘  └───────┘ │
└──────────────────────────────────────────────────────────────────────────┘
```

### 9.2 Integration Points

**How CI/CD pipelines integrate with ComplianceService:**

1. **Pre-requisite**: Application must be registered with environment configs via the management API
2. **Pipeline step**: After security scans complete, the pipeline collects all scan results
3. **API call**: Pipeline POSTs to `/api/compliance/evaluate` with the application ID, target environment, and scan results
4. **Decision handling**: Pipeline reads the `passed` boolean from the response
   - `true` → continue to deployment
   - `false` → fail the pipeline, log violations from the response body
5. **Evidence**: The full evaluation result (including violations and vulnerability counts) can be attached to the pipeline artifacts

**Example pipeline pseudocode:**

```yaml
# GitHub Actions example
- name: Compliance Gate
  run: |
    RESULT=$(curl -s -X POST https://compliance-api/api/compliance/evaluate \
      -H "Content-Type: application/json" \
      -d '{ "applicationId": "${{ vars.APP_ID }}",
             "environment": "production",
             "scanResults": ${{ steps.scan.outputs.results }},
             "initiatedBy": "${{ github.actor }}" }')

    PASSED=$(echo $RESULT | jq -r '.passed')
    if [ "$PASSED" != "true" ]; then
      echo "Compliance check FAILED"
      echo $RESULT | jq '.policyDecision.violations'
      exit 1
    fi
```

---

## 10. Infrastructure and Deployment

### 10.1 Container Topology

```
┌─────────────────────────────────────────────────────────────┐
│                   Docker Compose / Kubernetes                │
│                   Network: compliance-network                │
│                                                              │
│  ┌──────────────────┐  ┌────────────────┐  ┌─────────────┐  │
│  │ compliance-api   │  │ compliance-opa │  │ compliance-  │  │
│  │                  │  │                │  │ postgres     │  │
│  │ ASP.NET Core 8.0 │  │ OPA Server    │  │              │  │
│  │ Port: 5000 (80)  │  │ Port: 8181    │  │ PostgreSQL 16│  │
│  │                  │  │               │  │ Port: 5432   │  │
│  │ Depends on:      │  │ Volumes:      │  │              │  │
│  │  - postgres ✓    │  │  ./policies   │  │ Volumes:     │  │
│  │  - opa ✓         │  │  (read-only)  │  │  pgdata      │  │
│  └──────────────────┘  └────────────────┘  └─────────────┘  │
│                                                              │
│  Health checks:                                              │
│  - API: curl /health                                         │
│  - OPA: wget /health                                         │
│  - PG:  pg_isready                                           │
└─────────────────────────────────────────────────────────────┘
```

### 10.2 Data Persistence

**PostgreSQL Schema** (managed by EF Core migrations):

| Table | Purpose | Key Fields |
|-------|---------|------------|
| `Applications` | Application profiles | Id, Name, Owner, IsActive, CreatedAt, UpdatedAt |
| `EnvironmentConfigs` | Per-environment settings | ApplicationId, EnvironmentName, RiskTier, SecurityTools (JSON), Policies (JSON) |
| `ComplianceEvaluations` | Evaluation records | ApplicationId, Environment, RiskTier, ScanResults (JSON), PolicyDecision (JSON), EvaluatedAt |
| `AuditLogs` | Immutable decision records | EvaluationId, ApplicationId, Allowed, Reason, Violations (JSON), Evidence (JSON), Vulnerability Counts |

**Database resilience features:**
- Automatic retry on transient failures (3 retries, 5-second delay)
- Connection string configured via environment variables for containerized deployments
- Migrations applied automatically in development; explicit scripts recommended for production

---

## 11. Security and Governance

### 11.1 Security Controls

| Control | Implementation | Status |
|---------|---------------|--------|
| **Input validation** | FluentValidation on all command inputs; domain-level invariant enforcement | Implemented |
| **Error sanitization** | GlobalExceptionMiddleware suppresses stack traces in non-development environments | Implemented |
| **Structured logging** | Serilog with context enrichment; no sensitive data logging | Implemented |
| **HTTPS enforcement** | `UseHttpsRedirection()` middleware | Implemented |
| **Health monitoring** | Health checks for PostgreSQL and OPA connectivity | Implemented |
| **Policy immutability** | OPA policies loaded read-only from volume mount | Implemented |
| **Audit evidence** | Full JSON evidence (scan input, policy input, policy output) stored per decision | Implemented |
| **Authentication/Authorization** | Not yet implemented | Gap |
| **API rate limiting** | Not yet implemented | Gap |
| **Secrets management** | Connection strings in appsettings (should use vault) | Gap |
| **Network segmentation** | Docker network isolation; OPA not publicly exposed | Partial |

### 11.2 Governance Framework

ComplianceService enforces governance through several mechanisms:

```
┌───────────────────────────────────────────────────────────────────┐
│                    GOVERNANCE LAYERS                               │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  POLICY-AS-CODE (Rego files in version control)             │  │
│  │  - Policies are reviewed via pull requests                  │  │
│  │  - Changes tracked in git history                           │  │
│  │  - Environment-specific rules enforced consistently         │  │
│  └─────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  RISK CLASSIFICATION (risk tiers per application)           │  │
│  │  - Critical: zero tolerance, all tools required             │  │
│  │  - High: near-zero tolerance, most tools required           │  │
│  │  - Medium/Low: monitoring and tracking mode                 │  │
│  └─────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  AUDIT TRAIL (immutable decision evidence)                  │  │
│  │  - Every allow/deny recorded with full payload              │  │
│  │  - Queryable by app, environment, risk tier, date range     │  │
│  │  - Statistics endpoint for compliance dashboards            │  │
│  │  - Evidence includes: scan data, OPA input, OPA output      │  │
│  └─────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  TOOL ENFORCEMENT (required security scanners per env)      │  │
│  │  - Production requires all designated scanners              │  │
│  │  - Missing scans = automatic denial                         │  │
│  │  - Prevents partial-scan deployments                        │  │
│  └─────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  NOTIFICATION & ALERTING                                    │  │
│  │  - Blocked deployments trigger owner notification           │  │
│  │  - Critical vulnerabilities trigger escalation alerts       │  │
│  │  - Extensible to Slack, PagerDuty, email, ITSM             │  │
│  └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────┘
```

### 11.3 Compliance Audit Trail

The audit trail is designed for regulatory compliance (SOC 2, PCI-DSS, HIPAA):

**What is recorded for every evaluation:**

| Field | Purpose |
|-------|---------|
| Evaluation ID | Unique identifier for cross-referencing |
| Application identity | Name, ID, owner at time of evaluation |
| Environment + Risk tier | What was being deployed and where |
| Decision (allow/deny) | The gate outcome |
| Reason | Human-readable explanation |
| Violations list | Specific policy rules that were violated |
| Scan results JSON | Raw input from security tools |
| OPA input JSON | Exact payload sent to policy engine |
| OPA output JSON | Exact response from policy engine |
| Vulnerability counts | Critical, high, medium, low tallies |
| Timestamp | When the evaluation occurred |

**This ensures:**
- Auditors can replay any decision with the exact data that was used
- No information is lost between scan results and the final decision
- Both the "what was evaluated" and "how it was decided" are preserved

### 11.4 Threat Model Considerations

| Threat | Mitigation | Recommendation |
|--------|-----------|----------------|
| **Bypassing the compliance gate** | Pipeline must be configured to call the service; currently enforcement is voluntary | Integrate with pipeline platform to make gate mandatory |
| **Tampering with scan results** | No signature verification on incoming scan data | Add HMAC or JWT signing of scan payloads from trusted CI runners |
| **OPA policy tampering** | Policies mounted read-only | Sign Rego bundles; verify at load time |
| **Unauthorized API access** | No authentication currently | Add OAuth 2.0 / API key authentication |
| **Data exfiltration via audit logs** | Audit logs contain vulnerability details | Apply RBAC to audit endpoints; encrypt at rest |
| **Denial of service** | No rate limiting | Add rate limiting middleware; implement circuit breakers |

---

## 12. Scalability and Growth

### 12.1 Current Architecture Scalability

```
                    SCALING CHARACTERISTICS
                    ═══════════════════════

Component          Stateless?   Horizontally     Bottleneck?
                                Scalable?
────────────────── ────────── ──────────────── ────────────
API Service        Yes          Yes (behind LB)  No
OPA Sidecar        Yes          Yes (per pod)    No (in-memory)
PostgreSQL         No           Read replicas    Yes (writes)
Policy Files       Static       Volume mount     No
```

### 12.2 Horizontal Scaling Strategy

```
                      SCALED DEPLOYMENT
                      ─────────────────

        ┌─────────────────────────────────────────┐
        │           Load Balancer                  │
        └──────────┬──────────┬──────────┬────────┘
                   │          │          │
          ┌────────▼──┐ ┌────▼──────┐ ┌─▼────────┐
          │ API Pod 1 │ │ API Pod 2 │ │ API Pod N │
          │ + OPA     │ │ + OPA     │ │ + OPA     │
          │ sidecar   │ │ sidecar   │ │ sidecar   │
          └─────┬─────┘ └────┬──────┘ └─────┬─────┘
                │            │              │
                └────────────┼──────────────┘
                             │
                    ┌────────▼─────────┐
                    │   PostgreSQL     │
                    │   Primary        │
                    │     │            │
                    │     ├── Read     │
                    │     │   Replica  │
                    │     │   1        │
                    │     │            │
                    │     └── Read     │
                    │         Replica  │
                    │         2        │
                    └──────────────────┘
```

**Key scaling strategies:**

1. **API + OPA pods**: Since both the API and OPA sidecar are stateless, they can be scaled horizontally behind a load balancer. Each pod contains its own OPA instance loaded with the same policies.

2. **Database read replicas**: Audit queries and reporting can be routed to read replicas while writes (evaluations, audit logs) go to the primary.

3. **OPA bundle distribution**: In production at scale, OPA can be configured to pull policy bundles from a central bundle server rather than relying on volume mounts, enabling policy updates without redeployment.

4. **Async processing**: Notifications are already fire-and-forget. Additional post-processing (metrics aggregation, webhook delivery) can be offloaded to a message queue.

### 12.3 Growth Roadmap

```
    CURRENT STATE                    NEAR-TERM                      LONG-TERM
    ─────────────                    ─────────                      ─────────

    Single instance               Multi-instance +               Event-driven +
    Docker Compose                Kubernetes                     Distributed

    ┌──────────────┐             ┌──────────────┐              ┌──────────────┐
    │ Monolith API │             │ K8s Deployment│              │ Microservices│
    │ + OPA        │      ──▶   │ HPA (auto-    │       ──▶   │ Event bus    │
    │ + PostgreSQL │             │  scale)       │              │ CQRS w/      │
    │              │             │ OPA per pod   │              │  separate    │
    │ Single DB    │             │ PG primary +  │              │  read/write  │
    │              │             │  replicas     │              │  stores      │
    └──────────────┘             └──────────────┘              └──────────────┘

    Supports:                    Supports:                      Supports:
    ~10 apps                     ~100-500 apps                  ~1000+ apps
    ~50 evals/day                ~5,000 evals/day               ~100,000+
                                                                 evals/day
```

**Potential growth features:**

| Feature | Purpose |
|---------|---------|
| **Multi-tenant support** | Serve multiple organizations from a single deployment |
| **Policy versioning** | Track policy changes over time; evaluate against specific versions |
| **Webhook callbacks** | Push evaluation results to external systems in real time |
| **Caching layer** | Cache application profiles and recent evaluations (Redis) |
| **Batch evaluation** | Evaluate multiple applications in a single request |
| **Custom policy authoring UI** | Non-developer policy editors for compliance teams |
| **Integration SDK** | Client libraries for common CI/CD platforms |
| **Metrics and dashboards** | Prometheus/Grafana integration for operational visibility |

### 12.4 Performance Considerations

| Aspect | Current Design | Optimization Path |
|--------|---------------|-------------------|
| **OPA evaluation latency** | Synchronous HTTP to local sidecar (~1-10ms) | Already optimal; local network call |
| **Database writes** | Two writes per evaluation (evaluation + audit log) | Batch writes; async audit log persistence |
| **Scan result payload size** | Full vulnerability list in request body | Consider streaming or chunked uploads for large scans |
| **Audit log growth** | Unbounded; stores full JSON evidence | Implement retention policies; archive to cold storage |
| **Query performance** | No explicit indices beyond EF Core defaults | Add indices on ApplicationId, Environment, EvaluatedAt, Allowed |

---

## 13. Evaluation and Maturity Assessment

### 13.1 Current State Assessment

| Dimension | Maturity | Notes |
|-----------|----------|-------|
| **Core functionality** | Solid | Full evaluation pipeline, domain model, and audit trail |
| **Architecture** | Strong | Clean Architecture with DDD, CQRS, proper layer separation |
| **Policy engine** | Functional | OPA integration with environment-specific Rego policies |
| **Data persistence** | Functional | EF Core with PostgreSQL, migrations in place |
| **Observability** | Partial | Serilog structured logging, health checks; no metrics/tracing |
| **Security** | Incomplete | Input validation present; authentication, authorization, and rate limiting absent |
| **Testing** | Not started | No test projects exist in the solution |
| **CI/CD pipeline** | Not started | `.github/workflows/` directory exists but is empty |
| **Documentation** | Strong | Comprehensive README files at every layer |
| **Notification system** | Stub | Logging-only implementation; no real notification channels |

### 13.2 Gap Analysis

```
    CRITICAL GAPS                    IMPORTANT GAPS                 NICE TO HAVE
    ═════════════                    ══════════════                 ════════════

    ┌──────────────────┐            ┌──────────────────┐          ┌──────────────┐
    │ No authentication│            │ No CI/CD pipeline│          │ No caching   │
    │ or authorization │            │ for the service  │          │ layer        │
    │                  │            │ itself           │          │              │
    │ No test suite    │            │                  │          │ No metrics / │
    │                  │            │ No Dockerfile    │          │ Prometheus   │
    │ CORS set to      │            │ for the API      │          │              │
    │ AllowAll         │            │                  │          │ No API       │
    │                  │            │ Notification svc │          │ versioning   │
    │ Secrets in       │            │ is stub-only     │          │              │
    │ appsettings.json │            │                  │          │ No request   │
    │                  │            │ No distributed   │          │ idempotency  │
    │ No rate limiting │            │ tracing          │          │              │
    └──────────────────┘            └──────────────────┘          └──────────────┘
```

### 13.3 Recommended Next Steps

**Priority 1 - Security Hardening:**
- Add authentication (OAuth 2.0 / API key) to all endpoints
- Restrict CORS to known origins
- Move secrets to a vault (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
- Add rate limiting to protect against abuse

**Priority 2 - Quality Assurance:**
- Create unit test project for domain logic and command handlers
- Create integration test project for API and OPA integration
- Add policy testing framework for Rego policies (OPA test runner)

**Priority 3 - Deployment Readiness:**
- Create a Dockerfile for the API service
- Build CI/CD pipeline (GitHub Actions) for build, test, container publish
- Add database migration strategy for production (explicit scripts, not auto-migrate)

**Priority 4 - Operational Maturity:**
- Implement real notification channels (Slack, email, PagerDuty)
- Add distributed tracing (OpenTelemetry)
- Add Prometheus metrics endpoint
- Implement log retention and audit archive policies

---

## 14. Technology Stack

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| Runtime | .NET | 8.0 LTS | Application framework |
| Language | C# | 12 | Primary language with nullable reference types |
| Web Framework | ASP.NET Core | 8.0 | REST API hosting |
| Policy Engine | Open Policy Agent | 0.60+ | Rego policy evaluation |
| Database | PostgreSQL | 16 | Persistent data store |
| ORM | Entity Framework Core | 8.0 | Database access and migrations |
| DB Provider | Npgsql | 8.0 | PostgreSQL .NET driver |
| Mediator | MediatR | 12.2 | CQRS command/query dispatching |
| Validation | FluentValidation | 11.9 | Input validation |
| Logging | Serilog | 8.0 | Structured logging |
| API Docs | Swashbuckle | 6.5 | Swagger/OpenAPI generation |
| Containers | Docker Compose | 3.8 | Local development orchestration |

---

*Document generated: February 2026*
*Based on analysis of ComplianceService source code at `/shared/ComplianceService/`*
