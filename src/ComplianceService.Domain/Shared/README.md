# Shared Domain Primitives

**Foundation classes and patterns used across all bounded contexts.**

## Purpose

The Shared folder contains base classes and common patterns that all domain objects inherit from. These primitives provide:
- **Type safety** - Entities vs Value Objects are distinct types
- **Consistency** - All aggregates follow the same patterns
- **Reusability** - Don't repeat identity logic, equality logic, etc.
- **Domain event infrastructure** - Built into aggregate roots

This is also known as the **Shared Kernel** in DDD terminology.

## Core Components

### 1. Entity<TId>

**Purpose:** Base class for all objects with identity.

**File:** `Entity.cs`

**When to use:**
- Object has a unique identifier (Guid, int, string)
- Two objects with the same ID are the "same" object
- Object state can change over time
- Examples: EnvironmentConfig, Order, User

**Implementation:**
```csharp
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; }

    protected Entity(TId id)
    {
        Id = id;
    }

    // Equality based on ID
    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && Id.Equals(entity.Id);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
```

**Key characteristics:**
- `protected set` on Id - only entity itself can change ID (usually never changed)
- Equality by ID - if IDs match, entities are equal regardless of other properties
- Parameterless constructor for EF Core

**Usage example:**
```csharp
public class EnvironmentConfig : Entity<Guid>
{
    public string EnvironmentName { get; private set; }

    private EnvironmentConfig() : base() { } // EF Core

    private EnvironmentConfig(Guid id, string name) : base(id)
    {
        EnvironmentName = name;
    }
}

// Two environments with different properties but same ID are equal
var env1 = new EnvironmentConfig(guid, "production");
var env2 = new EnvironmentConfig(guid, "prod"); // typo in name
env1.Equals(env2); // TRUE - same ID
```

**Extending Entity:**

If you need additional base properties (CreatedBy, UpdatedBy):
```csharp
public abstract class AuditableEntity<TId> : Entity<TId>
    where TId : notnull
{
    public string CreatedBy { get; protected set; } = string.Empty;
    public string? UpdatedBy { get; protected set; }

    protected AuditableEntity(TId id) : base(id) { }
    protected AuditableEntity() : base() { }
}
```

### 2. ValueObject

**Purpose:** Base class for objects without identity (compared by value).

**File:** `ValueObject.cs`

**When to use:**
- Object has no unique identifier
- Two objects with same values are interchangeable
- Object is immutable
- Examples: RiskTier, EmailAddress, Money, Address

**Implementation:**
```csharp
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x != null ? x.GetHashCode() : 0)
            .Aggregate((x, y) => x ^ y);
    }
}
```

**Key characteristics:**
- No ID property
- Equality by comparing all properties
- Should be immutable (all properties private set or init)
- `GetEqualityComponents()` defines which properties matter for equality

**Usage example:**
```csharp
public sealed class RiskTier : ValueObject
{
    public string Value { get; }

    private RiskTier(string value)
    {
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value; // Only Value matters for equality
    }

    public static Result<RiskTier> Create(string value)
    {
        if (!IsValid(value))
            return Result.Failure<RiskTier>("Invalid risk tier");

        return Result.Success(new RiskTier(value.ToLowerInvariant()));
    }
}

// Usage
var tier1 = RiskTier.Create("critical").Value;
var tier2 = RiskTier.Create("CRITICAL").Value;
tier1.Equals(tier2); // TRUE - same value after normalization
```

**Complex value objects:**
```csharp
public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
        yield return Country;
        // All properties must match for equality
    }
}
```

**Value object guidelines:**
- Make sealed to prevent inheritance
- Private constructor + public static Create() method
- All properties should be readonly (private set or init)
- Include validation in Create() method
- Override ToString() for debugging

### 3. AggregateRoot<TId>

**Purpose:** Base class for aggregate roots (consistency boundaries).

**File:** `AggregateRoot.cs`

**When to use:**
- Object is the entry point to a cluster of related objects
- Object enforces consistency rules for itself and child entities
- Object needs to raise domain events
- Examples: Application, ComplianceEvaluation, AuditLog

**Implementation:**
```csharp
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    // Audit fields
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    protected void MarkAsUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**Key characteristics:**
- Inherits from Entity (has identity)
- Manages domain events
- Has audit timestamps (CreatedAt, UpdatedAt)
- Only aggregate roots should have repositories

**Usage example:**
```csharp
public class Application : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    private readonly List<EnvironmentConfig> _environments = new();

    public static Result<Application> Create(string name, ...)
    {
        var app = new Application(Guid.NewGuid(), name, ...);

        // Raise domain event
        app.AddDomainEvent(new ApplicationRegisteredEvent(
            app.Id,
            app.Name,
            DateTime.UtcNow));

        return Result.Success(app);
    }

    public void UpdateName(string newName)
    {
        Name = newName;
        MarkAsUpdated(); // Sets UpdatedAt timestamp
    }
}
```

**Aggregate rules:**
- Keep aggregates small (don't load entire object graph)
- Child entities accessed through aggregate root only
- External objects reference aggregate by ID, not object reference
- Enforce all invariants within aggregate boundary

**Example of enforcing invariants:**
```csharp
public class Application : AggregateRoot<Guid>
{
    private readonly List<EnvironmentConfig> _environments = new();

    public Result AddEnvironment(EnvironmentConfig env)
    {
        // Invariant: Environment name must be unique within application
        if (_environments.Any(e => e.EnvironmentName == env.EnvironmentName))
            return Result.Failure("Environment already exists");

        // Invariant: Application ID must match
        if (env.ApplicationId != Id)
            return Result.Failure("Environment belongs to different application");

        _environments.Add(env);
        MarkAsUpdated();
        return Result.Success();
    }
}
```

### 4. IDomainEvent

**Purpose:** Marker interface for domain events.

**File:** `IDomainEvent.cs`

**When to use:**
- Something interesting happened that other parts of the system might care about
- Decouple aggregates (Application doesn't depend on Audit)
- Enable asynchronous processing
- Support event sourcing

**Implementation:**
```csharp
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
```

**Usage example:**
```csharp
public sealed class ApplicationRegisteredEvent : IDomainEvent
{
    public Guid ApplicationId { get; }
    public string ApplicationName { get; }
    public DateTime OccurredOn { get; }

    public ApplicationRegisteredEvent(Guid appId, string name, DateTime occurredOn)
    {
        ApplicationId = appId;
        ApplicationName = name;
        OccurredOn = occurredOn;
    }
}
```

**Event naming conventions:**
- Past tense - "Registered", not "Register" (something that happened)
- Descriptive - clearly states what occurred
- Suffix with "Event" for clarity
- Immutable - all properties should be readonly

**Event flow:**
```
1. Aggregate raises event: app.AddDomainEvent(new ApplicationRegisteredEvent(...))
2. Repository saves aggregate to database
3. Infrastructure publishes events (via MediatR, message bus, etc.)
4. Handlers react to events:
   - Send email notification
   - Update read model
   - Create audit log
   - Trigger workflow
```

**Multiple events:**
```csharp
public Result UpdateRiskTierAndNotify(RiskTier newTier)
{
    var oldTier = RiskTier;
    RiskTier = newTier;

    // Raise multiple events
    AddDomainEvent(new RiskTierChangedEvent(Id, oldTier, newTier, DateTime.UtcNow));

    if (newTier.IsCritical && !oldTier.IsCritical)
    {
        // Additional event for escalation to critical
        AddDomainEvent(new ApplicationEscalatedToCriticalEvent(Id, DateTime.UtcNow));
    }

    return Result.Success();
}
```

### 5. Result / Result<T>

**Purpose:** Explicit success/failure handling without exceptions.

**File:** `Result.cs`

**When to use:**
- Operation can fail due to business rules (not programmer errors)
- Want to make failures explicit and type-safe
- Need to return error message to caller
- Avoid exceptions for control flow

**Implementation:**
```csharp
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, string.Empty);
    public static Result<T> Failure<T>(string error) => new(default!, false, error);
}

public class Result<T> : Result
{
    public T Value { get; }
}
```

**Usage patterns:**

**Creating results:**
```csharp
// Success without value
public Result Delete()
{
    if (HasActiveDeployments())
        return Result.Failure("Cannot delete with active deployments");

    IsDeleted = true;
    return Result.Success();
}

// Success with value
public static Result<Application> Create(string name, ...)
{
    if (string.IsNullOrWhiteSpace(name))
        return Result.Failure<Application>("Name required");

    return Result.Success(new Application(name, ...));
}
```

**Consuming results:**
```csharp
// Check and handle
var result = Application.Create(name, tier, owner);
if (result.IsFailure)
{
    logger.LogWarning($"Failed to create application: {result.Error}");
    return BadRequest(result.Error);
}

var application = result.Value;
await repository.AddAsync(application);

// Or pattern matching
return result switch
{
    { IsSuccess: true } => Ok(result.Value),
    { IsFailure: true } => BadRequest(result.Error),
    _ => throw new InvalidOperationException()
};
```

**Chaining operations (Railway-Oriented Programming):**
```csharp
public async Task<Result<ApplicationDto>> RegisterApplication(RegisterCommand cmd)
{
    var nameResult = ValidateName(cmd.Name);
    if (nameResult.IsFailure)
        return Result.Failure<ApplicationDto>(nameResult.Error);

    var tierResult = RiskTier.Create(cmd.RiskTier);
    if (tierResult.IsFailure)
        return Result.Failure<ApplicationDto>(tierResult.Error);

    var appResult = Application.Create(cmd.Name, tierResult.Value, cmd.Owner);
    if (appResult.IsFailure)
        return Result.Failure<ApplicationDto>(appResult.Error);

    await _repository.AddAsync(appResult.Value);

    return Result.Success(Map(appResult.Value));
}
```

**Extension methods for cleaner code:**
```csharp
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> mapper)
    {
        return result.IsSuccess
            ? Result.Success(mapper(result.Value))
            : Result.Failure<TOut>(result.Error);
    }

    public static async Task<Result<TOut>> Bind<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<Result<TOut>>> binder)
    {
        return result.IsSuccess
            ? await binder(result.Value)
            : Result.Failure<TOut>(result.Error);
    }
}

// Usage
var result = await Application.Create(name, tier, owner)
    .Map(app => new ApplicationDto(app))
    .Bind(dto => SaveToDatabase(dto));
```

## Design Patterns Explained

### Factory Method Pattern

**All domain objects should use static factory methods instead of public constructors.**

**Why:**
- Encapsulate creation logic
- Enforce validation before object creation
- Return Result to make failures explicit
- Can return existing instance (singleton pattern)

**Example:**
```csharp
public sealed class RiskTier : ValueObject
{
    // Singleton instances
    public static readonly RiskTier Critical = new("critical");
    public static readonly RiskTier High = new("high");

    // Private constructor - can't call `new RiskTier(...)`
    private RiskTier(string value)
    {
        Value = value;
    }

    // Public factory method
    public static Result<RiskTier> Create(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "critical" => Result.Success(Critical), // Return singleton
            "high" => Result.Success(High),
            _ => Result.Failure<RiskTier>("Invalid risk tier")
        };
    }
}
```

### Template Method Pattern

**Base classes define algorithm structure, subclasses fill in details.**

**Example in ValueObject:**
```csharp
public abstract class ValueObject
{
    // Template method - defines equality algorithm
    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents() // Call to abstract method
            .SequenceEqual(other.GetEqualityComponents());
    }

    // Abstract method - subclass provides components
    protected abstract IEnumerable<object?> GetEqualityComponents();
}

// Subclass implements details
public sealed class Address : ValueObject
{
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
    }
}
```

## Testing Shared Primitives

### Testing Entity Equality
```csharp
public class EntityTests
{
    [Fact]
    public void Entities_WithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var env1 = new EnvironmentConfig(id, "production");
        var env2 = new EnvironmentConfig(id, "staging");

        env1.Should().Be(env2); // Same ID = equal
    }

    [Fact]
    public void Entities_WithDifferentIds_ShouldNotBeEqual()
    {
        var env1 = new EnvironmentConfig(Guid.NewGuid(), "production");
        var env2 = new EnvironmentConfig(Guid.NewGuid(), "production");

        env1.Should().NotBe(env2); // Different IDs = not equal
    }
}
```

### Testing Value Object Equality
```csharp
public class ValueObjectTests
{
    [Fact]
    public void ValueObjects_WithSameValues_ShouldBeEqual()
    {
        var tier1 = RiskTier.Create("critical").Value;
        var tier2 = RiskTier.Create("critical").Value;

        tier1.Should().Be(tier2);
        tier1.GetHashCode().Should().Be(tier2.GetHashCode());
    }

    [Fact]
    public void ValueObjects_WithDifferentValues_ShouldNotBeEqual()
    {
        var tier1 = RiskTier.Create("critical").Value;
        var tier2 = RiskTier.Create("high").Value;

        tier1.Should().NotBe(tier2);
    }
}
```

### Testing Domain Events
```csharp
public class AggregateRootTests
{
    [Fact]
    public void WhenCreated_ShouldRaiseDomainEvent()
    {
        var app = Application.Create("test", RiskTier.Critical, "test@test.com").Value;

        app.DomainEvents.Should().HaveCount(1);
        app.DomainEvents.First().Should().BeOfType<ApplicationRegisteredEvent>();
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        var app = Application.Create("test", RiskTier.Critical, "test@test.com").Value;
        app.ClearDomainEvents();

        app.DomainEvents.Should().BeEmpty();
    }
}
```

## Common Mistakes to Avoid

### ❌ Making Value Objects Mutable
```csharp
public class EmailAddress : ValueObject
{
    public string Value { get; set; } // WRONG - should be private set or init

    // Can be changed after creation - breaks value object semantics
    var email = new EmailAddress { Value = "test@test.com" };
    email.Value = "different@test.com"; // Should not be possible
}
```

### ❌ Public Constructors on Aggregates
```csharp
public class Application : AggregateRoot<Guid>
{
    public Application(string name, ...) // WRONG - should be private
    {
        // Can't enforce validation if constructor is public
    }
}

// Can create invalid application
var app = new Application("", null, null); // Validation bypassed
```

### ❌ Returning Null Instead of Result
```csharp
public static Application? Create(string name) // WRONG
{
    if (string.IsNullOrWhiteSpace(name))
        return null; // Caller doesn't know WHY it failed

    return new Application(name);
}

// Better
public static Result<Application> Create(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return Result.Failure<Application>("Name cannot be empty");

    return Result.Success(new Application(name));
}
```

### ❌ Forgetting to Call Base Constructor
```csharp
public class Application : AggregateRoot<Guid>
{
    public Application(string name) // WRONG - forgot : base(Guid.NewGuid())
    {
        Name = name;
        // Id will be default(Guid) = 00000000-0000-0000-0000-000000000000
    }
}

// Correct
public Application(string name) : base(Guid.NewGuid())
{
    Name = name;
}
```

## Extending Shared Primitives

### Adding New Base Class

**Scenario:** Need auditable entity with CreatedBy/UpdatedBy

```csharp
public abstract class AuditableEntity<TId> : Entity<TId>
    where TId : notnull
{
    public string CreatedBy { get; protected set; } = string.Empty;
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    protected AuditableEntity(TId id, string createdBy) : base(id)
    {
        CreatedBy = createdBy;
    }

    protected AuditableEntity() : base() { }

    protected void MarkAsUpdated(string updatedBy)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

### Adding Result Extensions

```csharp
public static class ResultExtensions
{
    // Combine multiple results
    public static Result Combine(params Result[] results)
    {
        var failures = results.Where(r => r.IsFailure).ToList();
        if (failures.Any())
            return Result.Failure(string.Join("; ", failures.Select(f => f.Error)));

        return Result.Success();
    }

    // Execute action on success
    public static Result OnSuccess(this Result result, Action action)
    {
        if (result.IsSuccess)
            action();

        return result;
    }
}
```

## Performance Considerations

### Value Object Equality
- GetHashCode() is called frequently (dictionary lookups)
- Cache hash code if expensive to calculate
- Keep GetEqualityComponents() fast (avoid LINQ if possible)

### Domain Events
- Events stored in memory until published
- Clear events after publishing to prevent memory leaks
- Don't store large payloads in events (use references)

### Entity Tracking
- EF Core tracks entities by ID
- Keep IDs immutable (never change after creation)
- Use Guid.NewGuid() for new entities, not sequential IDs

---

**Remember:** These shared primitives are the foundation of your domain model. Changes here affect all bounded contexts, so modify carefully and maintain backwards compatibility.
