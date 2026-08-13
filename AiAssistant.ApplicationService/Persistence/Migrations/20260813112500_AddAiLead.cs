using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAssistant.ApplicationService.Persistence.Migrations;

public partial class AddAiLead : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AiLeads",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SourceLeadAssignmentId = table.Column<long>(type: "bigint", nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReportSubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReportDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiLeads", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AiLeads_SourceLeadAssignmentId",
            table: "AiLeads",
            column: "SourceLeadAssignmentId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AiLeads");
    }
}
