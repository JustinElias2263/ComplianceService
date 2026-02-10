package compliance.cicd.common

import future.keywords.if

# Common helper functions used across all policies

# Calculate total vulnerabilities across all scan results
total_vulnerabilities := count if {
    count := sum([
        result.criticalCount + result.highCount + result.mediumCount + result.lowCount |
        result := input.scanResults[_]
    ])
}

# Get list of all tools that were executed
tools_executed := tools if {
    tools := {result.tool | result := input.scanResults[_]}
}

# Check if a specific tool was executed
tool_was_executed(tool_name) if {
    some result in input.scanResults
    result.tool == tool_name
}

# Get all vulnerabilities of a specific severity
vulnerabilities_by_severity(severity) := vulns if {
    vulns := [v |
        result := input.scanResults[_]
        v := result.vulnerabilities[_]
        v.severity == severity
    ]
}

# Check if application is in a specific risk tier
is_risk_tier(tier) if {
    input.application.riskTier == tier
}

# Check if environment matches
is_environment(env) if {
    input.application.environment == env
}
