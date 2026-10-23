using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>support_requests: yêu cầu hỗ trợ nhân sự từ nhóm khác — Leader nhóm được xin xác nhận + chọn nhân viên (yêu cầu 2026-10-23).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261023110000_SupportRequests")]
    public partial class SupportRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    RequesterId = table.Column<int>(type: "int", nullable: false),
                    GroupLeaderId = table.Column<int>(type: "int", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(1500)", maxLength: 1500, nullable: true),
                    Confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    StaffIds = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    ConfirmedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_support_requests", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_support_requests_GroupLeaderId_Confirmed", table: "support_requests", columns: new[] { "GroupLeaderId", "Confirmed" });
            migrationBuilder.CreateIndex(name: "IX_support_requests_PlanId", table: "support_requests", column: "PlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "support_requests");
        }
    }
}
