package compliance.cicd.staging

import future.keywords.if

# Default deny
default allow := false

# Staging policy for HIGH risk tier
# Allow some high vulnerabilities but zero critical

# Main decision point
allow if {
    no_critical_vulnerabilities
    acceptable_high_vulnerabilities
    required_tools_executed
}

# Check for critical vulnerabilities
no_critical_vulnerabilities if {
    total_critical := sum([result.criticalCount | result := input.scanResults[_]])
    total_critical == 0
}

# Allow up to 3 high vulnerabilities in staging
acceptable_high_vulnerabilities if {
    total_high := sum([result.highCount | result := input.scanResults[_]])
    total_high <= 3
}

# Verify required security tools were executed
required_tools_executed if {
    required := {"snyk", "prisma-cloud"}  # Fewer requirements than production
    executed := {result.tool | result := input.scanResults[_]}
    count(required - executed) == 0  # All required tools must be present
}

# Violations array
violations[violation] {
    not no_critical_vulnerabilities
    total_critical := sum([result.criticalCount | result := input.scanResults[_]])
    violation := {
        "rule": "no_critical_vulnerabilities",
        "message": sprintf("Staging deployments must have zero critical vulnerabilities. Found: %d", [total_critical]),
        "severity": "critical"
    }
}

violations[violation] {
    not acceptable_high_vulnerabilities
    total_high := sum([result.highCount | result := input.scanResults[_]])
    violation := {
        "rule": "acceptable_high_vulnerabilities",
        "message": sprintf("Staging allows max 3 high vulnerabilities. Found: %d", [total_high]),
        "severity": "high"
    }
}

violations[violation] {
    not required_tools_executed
    required := {"snyk", "prisma-cloud"}
    executed := {result.tool | result := input.scanResults[_]}
    missing := required - executed
    violation := {
        "rule": "required_tools_executed",
        "message": sprintf("Missing required security scans: %v", [missing]),
        "severity": "high"
    }
}

# Reason for decision
reason := "Staging compliance checks passed" if allow
reason := "Staging compliance violations detected" if not allow
