using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>support_requests: Declined + DeclineReason — Leader nhóm được xin có thể từ chối (không đáp ứng được) — yêu cầu 2026-10-24.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261024110000_SupportDecline")]
    public partial class SupportDecline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "Declined", table: "support_requests", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "DeclineReason", table: "support_requests", type: "varchar(500)", maxLength: 500, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeclineReason", table: "support_requests");
            migrationBuilder.DropColumn(name: "Declined", table: "support_requests");
        }
    }
}
