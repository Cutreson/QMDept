using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ot_sends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    By = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Com = table.Column<int>(type: "int", nullable: false),
                    Bun = table.Column<int>(type: "int", nullable: false),
                    Nhe = table.Column<int>(type: "int", nullable: false),
                    Pc = table.Column<int>(type: "int", nullable: false),
                    People = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_sends", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    LeaderId = table.Column<int>(type: "int", nullable: true),
                    Level = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    SecurityStamp = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.Id);
                    table.ForeignKey(
                        name: "FK_people_people_LeaderId",
                        column: x => x.LeaderId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Factory = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Need = table.Column<int>(type: "int", nullable: false),
                    StatusOverride = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Closed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "day_plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Meal = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    LeaveType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    LeaveSession = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    LeaveFrom = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: true),
                    LeaveTo = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: true),
                    LeaveHours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    LeaveDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    TripProvince = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    TripCustomer = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    SupportScope = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    SupportText = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LeaderDecision = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    LeaderBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    LeaderAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LeaderNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    TpDecision = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    TpBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    TpAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TpNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_day_plans_people_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "day_reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Blocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LeaderDecision = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    LeaderBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    LeaderAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LeaderNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_day_reports_people_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "leader_reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    LeaderId = table.Column<int>(type: "int", nullable: false),
                    By = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    SummaryJson = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leader_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leader_reports_people_LeaderId",
                        column: x => x.LeaderId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "leaves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leaves_people_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    By = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    At = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assignments_people_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignments_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "overtimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DayType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Start = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    End = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Tier = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Break = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Exempt = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Meal = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Cost = table.Column<int>(type: "int", nullable: false),
                    Allowance = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    ExtraContent = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DecidedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Reg = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overtimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_overtimes_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_overtimes_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "plan_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DoneDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DoneBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_items_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "project_needs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Need = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_needs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_needs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "work_tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    PlanItemId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Start = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    End = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Selected = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    CarryOk = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CarriedFromId = table.Column<int>(type: "int", nullable: true),
                    CarriedToId = table.Column<int>(type: "int", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CarryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CarryBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CarryAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EditedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EditedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GivenBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ExtraId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_tasks_people_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_tasks_plan_items_PlanItemId",
                        column: x => x.PlanItemId,
                        principalTable: "plan_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_tasks_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "day_plan_tasks",
                columns: table => new
                {
                    DayPlanId = table.Column<int>(type: "int", nullable: false),
                    WorkTaskId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_plan_tasks", x => new { x.DayPlanId, x.WorkTaskId });
                    table.ForeignKey(
                        name: "FK_day_plan_tasks_day_plans_DayPlanId",
                        column: x => x.DayPlanId,
                        principalTable: "day_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_day_plan_tasks_work_tasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "work_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "extras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Start = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: true),
                    End = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    DecidedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DecidedRole = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_extras_people_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_extras_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_extras_work_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "work_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "overtime_tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OvertimeId = table.Column<int>(type: "int", nullable: false),
                    WorkTaskId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overtime_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_overtime_tasks_overtimes_OvertimeId",
                        column: x => x.OvertimeId,
                        principalTable: "overtimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_overtime_tasks_work_tasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "work_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StoredName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    ExtraId = table.Column<int>(type: "int", nullable: true),
                    UploadedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_photos_extras_ExtraId",
                        column: x => x.ExtraId,
                        principalTable: "extras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_photos_work_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "work_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_EmployeeId",
                table: "assignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_ProjectId_EmployeeId",
                table: "assignments",
                columns: new[] { "ProjectId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_day_plan_tasks_WorkTaskId",
                table: "day_plan_tasks",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_day_plans_Date_EmployeeId",
                table: "day_plans",
                columns: new[] { "Date", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_day_plans_EmployeeId",
                table: "day_plans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_day_reports_Date_EmployeeId",
                table: "day_reports",
                columns: new[] { "Date", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_day_reports_EmployeeId",
                table: "day_reports",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_extras_Date",
                table: "extras",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_extras_EmployeeId",
                table: "extras",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_extras_ProjectId",
                table: "extras",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_extras_TaskId",
                table: "extras",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_leader_reports_Date_LeaderId",
                table: "leader_reports",
                columns: new[] { "Date", "LeaderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leader_reports_LeaderId",
                table: "leader_reports",
                column: "LeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_leaves_Date_EmployeeId",
                table: "leaves",
                columns: new[] { "Date", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leaves_EmployeeId",
                table: "leaves",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ot_sends_Date",
                table: "ot_sends",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_overtime_tasks_OvertimeId",
                table: "overtime_tasks",
                column: "OvertimeId");

            migrationBuilder.CreateIndex(
                name: "IX_overtime_tasks_WorkTaskId",
                table: "overtime_tasks",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_overtimes_Date_PersonId",
                table: "overtimes",
                columns: new[] { "Date", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_overtimes_PersonId",
                table: "overtimes",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_overtimes_ProjectId",
                table: "overtimes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_people_Code",
                table: "people",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_people_LeaderId",
                table: "people",
                column: "LeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_photos_ExtraId",
                table: "photos",
                column: "ExtraId");

            migrationBuilder.CreateIndex(
                name: "IX_photos_TaskId",
                table: "photos",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_items_ProjectId",
                table: "plan_items",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_needs_Date_ProjectId",
                table: "project_needs",
                columns: new[] { "Date", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_needs_ProjectId",
                table: "project_needs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_Date_EmployeeId",
                table: "work_tasks",
                columns: new[] { "Date", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_EmployeeId",
                table: "work_tasks",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_ExtraId",
                table: "work_tasks",
                column: "ExtraId");

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_FromDate",
                table: "work_tasks",
                column: "FromDate");

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_PlanItemId",
                table: "work_tasks",
                column: "PlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_ProjectId",
                table: "work_tasks",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments");

            migrationBuilder.DropTable(
                name: "day_plan_tasks");

            migrationBuilder.DropTable(
                name: "day_reports");

            migrationBuilder.DropTable(
                name: "leader_reports");

            migrationBuilder.DropTable(
                name: "leaves");

            migrationBuilder.DropTable(
                name: "ot_sends");

            migrationBuilder.DropTable(
                name: "overtime_tasks");

            migrationBuilder.DropTable(
                name: "photos");

            migrationBuilder.DropTable(
                name: "project_needs");

            migrationBuilder.DropTable(
                name: "day_plans");

            migrationBuilder.DropTable(
                name: "overtimes");

            migrationBuilder.DropTable(
                name: "extras");

            migrationBuilder.DropTable(
                name: "work_tasks");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "plan_items");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
