using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>day_plans.SupportPick: lựa chọn xin hỗ trợ (JSON) ở báo cáo đầu ngày — yêu cầu 2026-10-15.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015120000_PlanSupportPick")]
    public partial class PlanSupportPick : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportPick",
                table: "day_plans",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportPick",
                table: "day_plans");
        }
    }
}
