using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceService.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial database migration
/// Creates tables for Application, EnvironmentConfig, ComplianceEvaluation, and AuditLog
/// </summary>
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create compliance schema
        migrationBuilder.EnsureSchema(name: "compliance");

        // Create Applications table
        migrationBuilder.CreateTable(
            name: "Applications",
            schema: "compliance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Applications", x => x.Id);
            },
            comment: "Application registry for compliance tracking");

        migrationBuilder.CreateIndex(
            name: "IX_Applications_Name",
            schema: "compliance",
            table: "Applications",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Applications_Owner",
            schema: "compliance",
            table: "Applications",
            column: "Owner");

        // Create EnvironmentConfigs table
        migrationBuilder.CreateTable(
            name: "EnvironmentConfigs",
            schema: "compliance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                RiskTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SecurityTools = table.Column<string>(type: "jsonb", nullable: false),
                PolicyReferences = table.Column<string>(type: "jsonb", nullable: false),
                Metadata = table.Column<string>(type: "jsonb", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EnvironmentConfigs", x => x.Id);
                table.ForeignKey(
                    name: "FK_EnvironmentConfigs_Applications_ApplicationId",
                    column: x => x.ApplicationId,
                    principalSchema: "compliance",
                    principalTable: "Applications",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EnvironmentConfigs_ApplicationId",
            schema: "compliance",
            table: "EnvironmentConfigs",
            column: "ApplicationId");

        migrationBuilder.CreateIndex(
            name: "IX_EnvironmentConfigs_ApplicationId_Name",
            schema: "compliance",
            table: "EnvironmentConfigs",
            columns: new[] { "ApplicationId", "Name" },
            unique: true);

        // Create ComplianceEvaluations table
        migrationBuilder.CreateTable(
            name: "ComplianceEvaluations",
            schema: "compliance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                RiskTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ScanResults = table.Column<string>(type: "jsonb", nullable: false),
                Decision = table.Column<string>(type: "jsonb", nullable: false),
                EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComplianceEvaluations", x => x.Id);
            },
            comment: "Compliance evaluation results with policy decisions");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceEvaluations_ApplicationId",
            schema: "compliance",
            table: "ComplianceEvaluations",
            column: "ApplicationId");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceEvaluations_EvaluatedAt",
            schema: "compliance",
            table: "ComplianceEvaluations",
            column: "EvaluatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceEvaluations_ApplicationId_Environment_EvaluatedAt",
            schema: "compliance",
            table: "ComplianceEvaluations",
            columns: new[] { "ApplicationId", "Environment", "EvaluatedAt" });

        // Create AuditLogs table
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            schema: "compliance",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EvaluationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                RiskTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Allowed = table.Column<bool>(type: "boolean", nullable: false),
                Reason = table.Column<string>(type: "text", nullable: false),
                Violations = table.Column<string>(type: "jsonb", nullable: false),
                Evidence = table.Column<string>(type: "jsonb", nullable: false),
                EvaluationDurationMs = table.Column<int>(type: "integer", nullable: false),
                EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CriticalCount = table.Column<int>(type: "integer", nullable: false),
                HighCount = table.Column<int>(type: "integer", nullable: false),
                MediumCount = table.Column<int>(type: "integer", nullable: false),
                LowCount = table.Column<int>(type: "integer", nullable: false),
                TotalVulnerabilityCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            },
            comment: "Immutable audit trail of compliance decisions - partitioned by timestamp (monthly) for scalability");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_ApplicationId",
            schema: "compliance",
            table: "AuditLogs",
            column: "ApplicationId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_ApplicationName",
            schema: "compliance",
            table: "AuditLogs",
            column: "ApplicationName");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Environment",
            schema: "compliance",
            table: "AuditLogs",
            column: "Environment");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_EvaluatedAt",
            schema: "compliance",
            table: "AuditLogs",
            column: "EvaluatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_EvaluationId",
            schema: "compliance",
            table: "AuditLogs",
            column: "EvaluationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_RiskTier",
            schema: "compliance",
            table: "AuditLogs",
            column: "RiskTier");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Allowed",
            schema: "compliance",
            table: "AuditLogs",
            column: "Allowed");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_ApplicationName_Environment_EvaluatedAt",
            schema: "compliance",
            table: "AuditLogs",
            columns: new[] { "ApplicationName", "Environment", "EvaluatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditLogs",
            schema: "compliance");

        migrationBuilder.DropTable(
            name: "ComplianceEvaluations",
            schema: "compliance");

        migrationBuilder.DropTable(
            name: "EnvironmentConfigs",
            schema: "compliance");

        migrationBuilder.DropTable(
            name: "Applications",
            schema: "compliance");

        migrationBuilder.DropSchema(name: "compliance");
    }
}
