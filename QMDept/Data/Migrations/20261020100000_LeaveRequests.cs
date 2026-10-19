using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>Đề xuất xin nghỉ (leave_requests) + chi tiết nghỉ trong bảng leaves — yêu cầu 2026-10-20.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261020100000_LeaveRequests")]
    public partial class LeaveRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "LeaveType", table: "leaves", type: "varchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "LeaveSession", table: "leaves", type: "varchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "LeaveFrom", table: "leaves", type: "varchar(5)", maxLength: 5, nullable: true);
            migrationBuilder.AddColumn<string>(name: "LeaveTo", table: "leaves", type: "varchar(5)", maxLength: 5, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "LeaveHours", table: "leaves", type: "decimal(9,2)", precision: 9, scale: 2, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "LeaveDays", table: "leaves", type: "decimal(9,2)", precision: 9, scale: 2, nullable: true);

            migrationBuilder.CreateTable(
                name: "leave_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    LeaveType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    LeaveSession = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    LeaveFrom = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    LeaveTo = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    LeaveHours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    LeaveDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DecidedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DecisionNote = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_requests", x => x.Id);
                    table.ForeignKey(name: "FK_leave_requests_people_PersonId", column: x => x.PersonId, principalTable: "people", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_leave_requests_PersonId", table: "leave_requests", column: "PersonId");
            migrationBuilder.CreateIndex(name: "IX_leave_requests_Date_PersonId", table: "leave_requests", columns: new[] { "Date", "PersonId" }, unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "leave_requests");
            foreach (var c in new[] { "LeaveType", "LeaveSession", "LeaveFrom", "LeaveTo", "LeaveHours", "LeaveDays" })
                migrationBuilder.DropColumn(name: c, table: "leaves");
        }
    }
}
