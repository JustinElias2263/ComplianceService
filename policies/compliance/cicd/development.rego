package compliance.cicd.development

import future.keywords.if

# Default allow (more permissive for development)
default allow := true

# Development policy for LOW/MEDIUM risk tier
# Very permissive - mainly for tracking and warnings

# Main decision point - block only on excessive critical vulnerabilities
allow if {
    acceptable_critical_vulnerabilities
}

# Allow up to 5 critical vulnerabilities in development (mainly for tracking)
acceptable_critical_vulnerabilities if {
    total_critical := sum([result.criticalCount | result := input.scanResults[_]])
    total_critical <= 5
}

# Violations array - mostly warnings
violations[violation] {
    not acceptable_critical_vulnerabilities
    total_critical := sum([result.criticalCount | result := input.scanResults[_]])
    violation := {
        "rule": "acceptable_critical_vulnerabilities",
        "message": sprintf("Even in development, %d critical vulnerabilities is excessive. Consider fixing.", [total_critical]),
        "severity": "medium"
    }
}

violations[violation] {
    # Warning if no security scans were run at all
    count(input.scanResults) == 0
    violation := {
        "rule": "security_scans_recommended",
        "message": "No security scans detected. Consider running at least one scan.",
        "severity": "low"
    }
}

# Reason for decision
reason := "Development environment - deployment allowed" if allow
reason := "Development environment - too many critical vulnerabilities" if not allow
