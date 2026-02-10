package compliance.cicd.production

import future.keywords.if

# Default deny
default allow := false

# Production policy for CRITICAL risk tier
# Zero tolerance for critical/high vulnerabilities

# Main decision point
allow if {
    no_critical_vulnerabilities
    no_high_vulnerabilities
    required_tools_executed
    no_license_violations
}

# Check for critical vulnerabilities
no_critical_vulnerabilities if {
    total_critical := sum([result.criticalCount | result := input.scanResults[_]])
    total_critical == 0
}

# Check for high vulnerabilities
no_high_vulnerabilities if {
    total_high := sum([result.highCount | result := input.scanResults[_]])
    total_high == 0
}

# Verify required security tools were executed
required_tools_executed if {
    required := {"snyk", "prisma-cloud", "sonarqube"}
    executed := {result.tool | result := input.scanResults[_]}
    required == executed
}

# Check for license violations (if tool provides this)
no_license_violations if {
    # Check if any scan result contains license violations
    count([v |
        result := input.scanResults[_]
        v := result.vulnerabilities[_]
        v.type == "license"
        v.severity == "high"
    ]) == 0
}

# Violations array - explain why deployment was denied
violations[violation] {
    not no_critical_vulnerabilities
    total_critical := sum([result.criticalCount | result := input.scanResults[_]])
    violation := {
        "rule": "no_critical_vulnerabilities",
        "message": sprintf("Production deployments must have zero critical vulnerabilities. Found: %d", [total_critical]),
        "severity": "critical"
    }
}

violations[violation] {
    not no_high_vulnerabilities
    total_high := sum([result.highCount | result := input.scanResults[_]])
    violation := {
        "rule": "no_high_vulnerabilities",
        "message": sprintf("Production deployments must have zero high vulnerabilities. Found: %d", [total_high]),
        "severity": "critical"
    }
}

violations[violation] {
    not required_tools_executed
    required := {"snyk", "prisma-cloud", "sonarqube"}
    executed := {result.tool | result := input.scanResults[_]}
    missing := required - executed
    violation := {
        "rule": "required_tools_executed",
        "message": sprintf("Missing required security scans: %v", [missing]),
        "severity": "critical"
    }
}

violations[violation] {
    not no_license_violations
    license_violations := [v |
        result := input.scanResults[_]
        v := result.vulnerabilities[_]
        v.type == "license"
        v.severity == "high"
    ]
    violation := {
        "rule": "no_license_violations",
        "message": sprintf("Found %d high-severity license violations", [count(license_violations)]),
        "severity": "high"
    }
}

# Reason for decision
reason := "All production compliance checks passed" if allow
reason := "Production compliance violations detected" if not allow
