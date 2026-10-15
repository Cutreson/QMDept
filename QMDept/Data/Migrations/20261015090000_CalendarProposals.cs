using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>Đề xuất lịch của NV (calendar_proposals + calendar_proposal_people) — yêu cầu 2026-10-15.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015090000_CalendarProposals")]
    public partial class CalendarProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_proposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    ProposerId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    PlanItemId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Start = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    End = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    DecidedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calendar_proposals_people_ProposerId",
                        column: x => x.ProposerId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_calendar_proposals_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "calendar_proposal_people",
                columns: table => new
                {
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_proposal_people", x => new { x.ProposalId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_calendar_proposal_people_calendar_proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "calendar_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calendar_proposal_people_people_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_proposals_Date",
                table: "calendar_proposals",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_proposals_ProjectId",
                table: "calendar_proposals",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_proposals_ProposerId",
                table: "calendar_proposals",
                column: "ProposerId");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_proposal_people_EmployeeId",
                table: "calendar_proposal_people",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "calendar_proposal_people");
            migrationBuilder.DropTable(name: "calendar_proposals");
        }
    }
}
