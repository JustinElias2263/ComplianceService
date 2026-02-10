using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.ApplicationProfile.ValueObjects;

/// <summary>
/// Supported security scanning tools
/// </summary>
public sealed class SecurityToolType : ValueObject
{
    public string Name { get; }

    public static readonly SecurityToolType Snyk = new("snyk");
    public static readonly SecurityToolType PrismaCloud = new("prismacloud");

    private SecurityToolType(string name)
    {
        Name = name;
    }

    public static Result<SecurityToolType> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<SecurityToolType>("Security tool name cannot be empty");

        var normalized = name.Trim().ToLowerInvariant();

        return normalized switch
        {
            "snyk" => Result.Success(Snyk),
            "prismacloud" => Result.Success(PrismaCloud),
            "prisma" => Result.Success(PrismaCloud), // Allow shorthand
            _ => Result.Failure<SecurityToolType>($"Unsupported security tool: {name}")
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
    }

    public override string ToString() => Name;
}
