using ComplianceService.Domain.ApplicationProfile.Entities;
using ComplianceService.Domain.ApplicationProfile.Events;
using ComplianceService.Domain.ApplicationProfile.ValueObjects;
using ComplianceService.Domain.Shared;

namespace ComplianceService.Domain.ApplicationProfile;

/// <summary>
/// Application Aggregate Root
/// Represents an application with its compliance configuration
/// </summary>
public class Application : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Owner { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<EnvironmentConfig> _environments = new();
    public IReadOnlyCollection<EnvironmentConfig> Environments => _environments.AsReadOnly();

    private Application() : base()
    {
        Name = string.Empty;
        Owner = string.Empty;
    }

    private Application(
        Guid id,
        string name,
        string owner) : base(id)
    {
        Name = name;
        Owner = owner;
        IsActive = true;
    }

    public static Result<Application> Create(
        string name,
        string owner)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Application>("Application name cannot be empty");

        if (name.Length < 3 || name.Length > 100)
            return Result.Failure<Application>("Application name must be between 3 and 100 characters");

        if (string.IsNullOrWhiteSpace(owner))
            return Result.Failure<Application>("Application owner cannot be empty");

        if (!IsValidEmail(owner))
            return Result.Failure<Application>("Owner must be a valid email address");

        var id = Guid.NewGuid();
        var application = new Application(id, name.Trim(), owner.Trim());

        application.AddDomainEvent(new ApplicationRegisteredEvent(
            id,
            application.Name,
            application.Owner,
            DateTime.UtcNow));

        return Result.Success(application);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public Result AddEnvironment(EnvironmentConfig environmentConfig)
    {
        if (environmentConfig.ApplicationId != Id)
            return Result.Failure("Environment config application ID does not match this application");

        if (_environments.Any(e => e.EnvironmentName == environmentConfig.EnvironmentName))
            return Result.Failure($"Environment '{environmentConfig.EnvironmentName}' already exists");

        _environments.Add(environmentConfig);
        MarkAsUpdated();

        return Result.Success();
    }

    public Result<EnvironmentConfig> GetEnvironment(string environmentName)
    {
        var normalized = environmentName.Trim().ToLowerInvariant();
        var environment = _environments.FirstOrDefault(e => e.EnvironmentName == normalized);

        if (environment == null)
            return Result.Failure<EnvironmentConfig>($"Environment '{environmentName}' not found");

        return Result.Success(environment);
    }

    public Result RemoveEnvironment(string environmentName)
    {
        var normalized = environmentName.Trim().ToLowerInvariant();
        var environment = _environments.FirstOrDefault(e => e.EnvironmentName == normalized);

        if (environment == null)
            return Result.Failure($"Environment '{environmentName}' not found");

        _environments.Remove(environment);
        MarkAsUpdated();

        return Result.Success();
    }

    public void UpdateOwner(string newOwner)
    {
        if (string.IsNullOrWhiteSpace(newOwner))
            throw new ArgumentException("Owner cannot be empty");

        if (!IsValidEmail(newOwner))
            throw new ArgumentException("Owner must be a valid email address");

        if (Owner != newOwner)
        {
            Owner = newOwner.Trim();
            MarkAsUpdated();
        }
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }
}
