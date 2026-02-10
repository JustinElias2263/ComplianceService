# OPA Policy Examples for ComplianceService

This directory contains example Open Policy Agent (Rego) policies for the ComplianceService.

## Directory Structure

```
policies/
├── compliance/
│   └── cicd/
│       ├── production.rego      # Critical risk tier policies
│       ├── staging.rego         # High risk tier policies
│       ├── development.rego     # Low/Medium risk tier policies
│       └── common.rego          # Shared policy functions
└── data/
    └── tool_requirements.json   # Required security tools per environment
```

## Policy Loading

Load policies into OPA sidecar:

```bash
# Option 1: Volume mount in Docker
docker run -v $(pwd)/policies:/policies openpolicyagent/opa:latest \
  run --server --addr=0.0.0.0:8181 /policies

# Option 2: Bundle server
opa run --server --addr=0.0.0.0:8181 \
  --set bundles.compliance.service=http://policy-server/bundles/compliance.tar.gz
```

## Policy Evaluation Flow

1. CI/CD pipeline runs security scans (Snyk, Prisma Cloud, etc.)
2. Pipeline calls ComplianceService API with scan results
3. ComplianceService forwards to OPA with environment context
4. OPA evaluates against environment-specific policy (e.g., `compliance.cicd.production`)
5. OPA returns allow/deny decision with violations
6. ComplianceService stores audit log and returns decision to pipeline

## Example Policies

See individual `.rego` files for implementation details.
