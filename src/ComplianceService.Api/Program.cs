using ComplianceService.Api.Middleware;
using ComplianceService.Application;
using ComplianceService.Infrastructure;
using FluentValidation.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/compliance-service-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddFluentValidation();

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Compliance Service API",
        Version = "v1",
        Description = "Policy Gateway for CI/CD Pipeline Compliance",
        Contact = new()
        {
            Name = "DevSecOps Team",
            Email = "devsecops@example.com"
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("ComplianceDatabase")!,
        name: "postgresql",
        tags: new[] { "db", "postgresql" })
    .AddUrlGroup(
        new Uri($"{builder.Configuration["OpaSettings:BaseUrl"]}/health"),
        name: "opa-sidecar",
        tags: new[] { "external", "opa" });

// Add CORS if needed
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Compliance Service API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });

    // Apply migrations in development
    // WARNING: Do not use in production!
    try
    {
        ComplianceService.Infrastructure.DependencyInjection.ApplyMigrations(app.Services);
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to apply database migrations");
    }
}

// Use HTTPS redirection
app.UseHttpsRedirection();

// Use custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

// Use CORS
app.UseCors("AllowAll");

// Use authorization
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map health checks
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

Log.Information("Compliance Service API starting...");

try
{
    app.Run();
    Log.Information("Compliance Service API stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Compliance Service API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
