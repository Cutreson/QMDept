using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>calendar_proposals.Departments: phòng ban hỗ trợ được đề xuất (thiết kế cơ / điện, lắp ráp cơ / điện) — yêu cầu 2026-10-15.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015100000_ProposalDepartments")]
    public partial class ProposalDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Departments",
                table: "calendar_proposals",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Departments",
                table: "calendar_proposals");
        }
    }
}
