namespace ComplianceService.Application.DTOs;

/// <summary>
/// Application profile DTO
/// </summary>
public class ApplicationDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Owner { get; init; }
    public required IReadOnlyList<EnvironmentConfigDto> Environments { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Environment configuration DTO
/// </summary>
public class EnvironmentConfigDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string RiskTier { get; init; }
    public required IReadOnlyList<string> SecurityTools { get; init; }
    public required IReadOnlyList<string> PolicyReferences { get; init; }
    public bool IsActive { get; init; }
}
